using System;
using System.Collections.Generic;
using System.Linq;
using SharpVk;
using SharpVk.Khronos;
using SkiaSharp;

namespace AvaloniaOpenGLHost.Rendering;

/// <summary>
/// SkiaSharp を Vulkan バックエンドで使用するレンダラー基底クラス。
/// プラットフォーム固有のサーフェス生成やインスタンス拡張は派生クラスが担当します。
/// </summary>
public abstract class SkiaVulkanRendererBase : GlRendererBase
{
    private Instance? _instance;
    private Surface? _surface;
    private PhysicalDevice? _physicalDevice;
    private Device? _device;
    private Queue? _graphicsQueue;
    private Queue? _presentQueue;
    private Swapchain? _swapchain;
    private Format _swapchainFormat;
    private Extent2D _swapchainExtent;
    private Image[]? _swapchainImages;
    private ImageView[]? _swapchainImageViews;
    private ImageLayout[]? _imageLayouts;
    private CommandPool? _commandPool;

    private GRSharpVkBackendContext? _backendContext;
    private GRVkExtensions? _vkExtensions;
    private GRContext? _grContext;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surfaceSkia;
    private uint _currentImageIndex;

    private string[]? _instanceExtensions;
    private string[]? _deviceExtensions;
    private SharingMode _swapchainSharingMode = SharingMode.Exclusive;
    private uint _graphicsQueueFamilyIndex;
    private uint _presentQueueFamilyIndex;

    protected override void PlatformInitialize(IntPtr windowHandle)
    {
        _instanceExtensions = BuildInstanceExtensions().ToArray();
        _deviceExtensions = BuildDeviceExtensions().ToArray();

        var appInfo = new ApplicationInfo
        {
            ApplicationName = "AvaloniaVulkanHost",
            ApplicationVersion = new Version(1, 0, 0),
            EngineName = "AvaloniaOpenGLHost",
            EngineVersion = new Version(1, 0, 0),
            ApiVersion = new Version(1, 2, 0)
        };

        _instance = Instance.Create(enabledExtensionNames: _instanceExtensions, applicationInfo: appInfo);
        _surface = CreateSurface(_instance, windowHandle);

        SelectPhysicalDevice();
        CreateLogicalDevice();
        CreateCommandPool();
        CreateSwapchainResources();
        CreateSkiaContext();
    }

    protected override void PlatformCleanup()
    {
        if (_device != null)
        {
            try
            {
                _device.WaitIdle();
            }
            catch
            {
                // ignore cleanup exceptions from WaitIdle
            }
        }

        DisposeSkiaSurface();
        DisposeSkiaContext();
        DestroySwapchain();

        _commandPool?.Dispose();
        _commandPool = null;

        _device?.Dispose();
        _device = null;
        _graphicsQueue = null;
        _presentQueue = null;

        DestroySurface();

        _instance?.Dispose();
        _instance = null;
    }

    protected override void PlatformResize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        if (_device == null || _surface == null)
            return;

        _device.WaitIdle();
        DisposeSkiaSurface();
        DestroySwapchain();
        CreateSwapchainResources((uint)width, (uint)height);
    }

    protected override void PlatformRender()
    {
        if (_device == null || _swapchain == null || _swapchainImages == null || _grContext == null || _graphicsQueue == null || _presentQueue == null)
            return;

        if (_swapchainExtent.Width == 0 || _swapchainExtent.Height == 0)
            return;

        uint imageIndex;
        try
        {
            imageIndex = _swapchain.AcquireNextImage(ulong.MaxValue, null, null);
        }
        catch (SharpVkException ex) when (ex.Result == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapchain();
            return;
        }

        var image = _swapchainImages[imageIndex];
        var previousLayout = _imageLayouts![imageIndex];

        if (previousLayout != ImageLayout.ColorAttachmentOptimal)
        {
            TransitionImageLayout(image, previousLayout, ImageLayout.ColorAttachmentOptimal);
            _imageLayouts[imageIndex] = ImageLayout.ColorAttachmentOptimal;
        }

        if (!EnsureSkiaSurface(imageIndex))
            return;

        DrawFrame(_surfaceSkia!);
        _surfaceSkia!.Canvas.Flush();
        _grContext.Flush();
        _graphicsQueue.WaitIdle();

        TransitionImageLayout(image, ImageLayout.ColorAttachmentOptimal, ImageLayout.PresentSrc);
        _imageLayouts[imageIndex] = ImageLayout.PresentSrc;

        try
        {
            _presentQueue.Present(ArrayProxy<Semaphore>.Null, new[] { _swapchain }, new[] { imageIndex });
        }
        catch (SharpVkException ex) when (ex.Result == Result.ErrorOutOfDateKhr || ex.Result == Result.SuboptimalKhr)
        {
            RecreateSwapchain();
        }
        finally
        {
            _presentQueue.WaitIdle();
        }
    }

    protected abstract Surface CreateSurface(Instance instance, IntPtr windowHandle);

    protected virtual void DestroySurface()
    {
        _surface?.Dispose();
        _surface = null;
    }

    protected abstract IEnumerable<string> GetPlatformInstanceExtensions();

    protected virtual IEnumerable<string> GetPlatformDeviceExtensions()
    {
        yield break;
    }

    protected virtual SKColorType GetColorTypeForFormat(Format format) => format switch
    {
        Format.B8G8R8A8Unorm or Format.B8G8R8A8Srgb => SKColorType.Bgra8888,
        Format.R8G8B8A8Unorm or Format.R8G8B8A8Srgb => SKColorType.Rgba8888,
        _ => SKColorType.Rgba8888
    };

    private IEnumerable<string> BuildInstanceExtensions()
    {
        yield return "VK_KHR_surface";
        foreach (var ext in GetPlatformInstanceExtensions())
            yield return ext;
    }

    private IEnumerable<string> BuildDeviceExtensions()
    {
        yield return "VK_KHR_swapchain";
        foreach (var ext in GetPlatformDeviceExtensions())
            yield return ext;
    }

    private void SelectPhysicalDevice()
    {
        var devices = _instance!.EnumeratePhysicalDevices();
        foreach (var device in devices)
        {
            if (!IsDeviceSuitable(device))
                continue;

            _physicalDevice = device;
            return;
        }

        throw new InvalidOperationException("Failed to find a suitable Vulkan GPU.");
    }

    private bool IsDeviceSuitable(PhysicalDevice device)
    {
        var indices = FindQueueFamilies(device);
        if (!indices.Graphics.HasValue || !indices.Present.HasValue)
            return false;

        if (!CheckExtensionSupport(device))
            return false;

        var swapchain = QuerySwapchainSupport(device);
        if (swapchain.Formats.Length == 0 || swapchain.PresentModes.Length == 0)
            return false;

        return true;
    }

    private (uint? Graphics, uint? Present) FindQueueFamilies(PhysicalDevice device)
    {
        uint? graphics = null;
        uint? present = null;

        var properties = device.GetQueueFamilyProperties();
        for (uint i = 0; i < properties.Length; i++)
        {
            if (properties[i].QueueFlags.HasFlag(QueueFlags.Graphics))
                graphics = i;

            if (device.GetSurfaceSupport(i, _surface!))
                present = i;

            if (graphics.HasValue && present.HasValue)
                break;
        }

        return (graphics, present);
    }

    private bool CheckExtensionSupport(PhysicalDevice device)
    {
        var available = device.EnumerateDeviceExtensionProperties()
            .Select(prop => prop.ExtensionName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var required in _deviceExtensions ?? Array.Empty<string>())
        {
            if (!available.Contains(required))
                return false;
        }

        return true;
    }

    private SwapchainSupportDetails QuerySwapchainSupport(PhysicalDevice device)
    {
        return new SwapchainSupportDetails
        {
            Capabilities = device.GetSurfaceCapabilities(_surface!),
            Formats = device.GetSurfaceFormats(_surface!),
            PresentModes = device.GetSurfacePresentModes(_surface!)
        };
    }

    private void CreateLogicalDevice()
    {
        var indices = FindQueueFamilies(_physicalDevice!);
        _graphicsQueueFamilyIndex = indices.Graphics!.Value;
        _presentQueueFamilyIndex = indices.Present!.Value;

        var uniqueFamilies = new HashSet<uint> { _graphicsQueueFamilyIndex, _presentQueueFamilyIndex };
        var queueInfos = new List<DeviceQueueCreateInfo>();
        const float priority = 1f;
        foreach (var family in uniqueFamilies)
        {
            queueInfos.Add(new DeviceQueueCreateInfo
            {
                QueueFamilyIndex = family,
                QueuePriorities = new[] { priority }
            });
        }

        var deviceExtensions = _deviceExtensions ?? Array.Empty<string>();
        _device = _physicalDevice!.CreateDevice(queueInfos.ToArray(), enabledExtensionNames: deviceExtensions);
        _graphicsQueue = _device.GetQueue(_graphicsQueueFamilyIndex, 0);
        _presentQueue = _device.GetQueue(_presentQueueFamilyIndex, 0);
    }

    private void CreateCommandPool()
    {
        _commandPool = _device!.CreateCommandPool(_graphicsQueueFamilyIndex, CommandPoolCreateFlags.ResetCommandBuffer);
    }

    private void CreateSwapchainResources(uint desiredWidth = 0, uint desiredHeight = 0)
    {
        var details = QuerySwapchainSupport(_physicalDevice!);
        var surfaceFormat = ChooseSurfaceFormat(details.Formats);
        var presentMode = ChoosePresentMode(details.PresentModes);
        var extent = ChooseExtent(details.Capabilities, desiredWidth, desiredHeight);

        uint imageCount = details.Capabilities.MinImageCount + 1;
        if (details.Capabilities.MaxImageCount > 0 && imageCount > details.Capabilities.MaxImageCount)
            imageCount = details.Capabilities.MaxImageCount;

        var queueFamilies = new[] { _graphicsQueueFamilyIndex, _presentQueueFamilyIndex };
        if (_graphicsQueueFamilyIndex != _presentQueueFamilyIndex)
        {
            _swapchainSharingMode = SharingMode.Concurrent;
        }
        else
        {
            _swapchainSharingMode = SharingMode.Exclusive;
        }

        _swapchain = _device!.CreateSwapchain(
            _surface!,
            imageCount,
            surfaceFormat.Format,
            surfaceFormat.ColorSpace,
            extent,
            1,
            ImageUsageFlags.ColorAttachment,
            _swapchainSharingMode,
            _swapchainSharingMode == SharingMode.Concurrent ? queueFamilies : null,
            details.Capabilities.CurrentTransform,
            ChooseCompositeAlpha(details.Capabilities.SupportedCompositeAlpha),
            presentMode,
            true,
            null);

        _swapchainFormat = surfaceFormat.Format;
        _swapchainExtent = extent;
        _swapchainImages = _swapchain.GetImages();
        _imageLayouts = new ImageLayout[_swapchainImages.Length];
        for (int i = 0; i < _imageLayouts.Length; i++)
            _imageLayouts[i] = ImageLayout.Undefined;

        _swapchainImageViews = new ImageView[_swapchainImages.Length];
        for (int i = 0; i < _swapchainImages.Length; i++)
        {
            var components = new ComponentMapping(ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity);
            var range = new ImageSubresourceRange(ImageAspectFlags.Color, 0, 1, 0, 1);
            _swapchainImageViews[i] = _device.CreateImageView(_swapchainImages[i], ImageViewType.ImageView2d, _swapchainFormat, components, range);
        }
    }

    private void CreateSkiaContext()
    {
        _backendContext = new GRSharpVkBackendContext
        {
            VkInstance = _instance!,
            VkPhysicalDevice = _physicalDevice!,
            VkDevice = _device!,
            VkQueue = _graphicsQueue!,
            GraphicsQueueIndex = _graphicsQueueFamilyIndex,
            GetProcedureAddress = GetSharpVkProc
        };

        var baseContext = (GRVkBackendContext)_backendContext;
        _vkExtensions = GRVkExtensions.Create(baseContext.GetProcedureAddress, baseContext.VkInstance, baseContext.VkPhysicalDevice, _instanceExtensions ?? Array.Empty<string>(), _deviceExtensions ?? Array.Empty<string>());
        baseContext.Extensions = _vkExtensions;

        _grContext = GRContext.CreateVulkan(_backendContext);
    }

    private void DisposeSkiaContext()
    {
        _surfaceSkia?.Dispose();
        _surfaceSkia = null;

        _renderTarget?.Dispose();
        _renderTarget = null;

        _grContext?.Dispose();
        _grContext = null;

        _vkExtensions?.Dispose();
        _vkExtensions = null;

        _backendContext?.Dispose();
        _backendContext = null;
    }

    private bool EnsureSkiaSurface(uint imageIndex)
    {
        if (_surfaceSkia != null && _currentImageIndex == imageIndex)
            return true;

        DisposeSkiaSurface();

        if (_grContext == null || _swapchainImages == null)
            return false;

        var info = new GRVkImageInfo
        {
            Image = _swapchainImages[imageIndex].RawHandle.ToUInt64(),
            ImageTiling = (uint)ImageTiling.Optimal,
            ImageLayout = (uint)ImageLayout.ColorAttachmentOptimal,
            Format = (uint)_swapchainFormat,
            ImageUsageFlags = (uint)ImageUsageFlags.ColorAttachment,
            SampleCount = 1,
            LevelCount = 1,
            CurrentQueueFamily = _graphicsQueueFamilyIndex,
            SharingMode = (uint)_swapchainSharingMode
        };

        _renderTarget = new GRBackendRenderTarget((int)_swapchainExtent.Width, (int)_swapchainExtent.Height, 0, info);
        _surfaceSkia = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.TopLeft, GetColorTypeForFormat(_swapchainFormat));
        _currentImageIndex = imageIndex;

        return _surfaceSkia != null;
    }

    private void DisposeSkiaSurface()
    {
        _surfaceSkia?.Dispose();
        _surfaceSkia = null;

        _renderTarget?.Dispose();
        _renderTarget = null;
    }

    private void DestroySwapchain()
    {
        if (_swapchainImageViews != null)
        {
            foreach (var view in _swapchainImageViews)
            {
                view?.Dispose();
            }
        }

        _swapchainImageViews = null;
        _swapchainImages = null;
        _imageLayouts = null;

        _swapchain?.Dispose();
        _swapchain = null;
    }

    private void RecreateSwapchain()
    {
        if (_device == null || _surface == null)
            return;

        _device.WaitIdle();
        DisposeSkiaSurface();
        DestroySwapchain();
        CreateSwapchainResources();
    }

    private SurfaceFormat ChooseSurfaceFormat(IReadOnlyList<SurfaceFormat> formats)
    {
        foreach (var format in formats)
        {
            if ((format.Format == Format.B8G8R8A8Unorm || format.Format == Format.B8G8R8A8Srgb) && format.ColorSpace == ColorSpace.SrgbNonlinear)
            {
                return format;
            }
        }

        return formats.First();
    }

    private PresentMode ChoosePresentMode(IReadOnlyList<PresentMode> modes)
    {
        if (modes.Contains(PresentMode.Mailbox))
            return PresentMode.Mailbox;
        if (modes.Contains(PresentMode.Fifo))
            return PresentMode.Fifo;
        return PresentMode.Immediate;
    }

    private Extent2D ChooseExtent(SurfaceCapabilities capabilities, uint desiredWidth, uint desiredHeight)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
            return capabilities.CurrentExtent;

        uint width = desiredWidth != 0 ? desiredWidth : capabilities.MinImageExtent.Width;
        uint height = desiredHeight != 0 ? desiredHeight : capabilities.MinImageExtent.Height;

        width = Math.Max(capabilities.MinImageExtent.Width, Math.Min(capabilities.MaxImageExtent.Width, width));
        height = Math.Max(capabilities.MinImageExtent.Height, Math.Min(capabilities.MaxImageExtent.Height, height));

        return new Extent2D { Width = width, Height = height };
    }

    private CompositeAlphaFlags ChooseCompositeAlpha(CompositeAlphaFlags supported)
    {
        if (supported.HasFlag(CompositeAlphaFlags.Opaque))
            return CompositeAlphaFlags.Opaque;
        if (supported.HasFlag(CompositeAlphaFlags.PreMultiplied))
            return CompositeAlphaFlags.PreMultiplied;
        if (supported.HasFlag(CompositeAlphaFlags.PostMultiplied))
            return CompositeAlphaFlags.PostMultiplied;
        return CompositeAlphaFlags.Inherit;
    }

    private void TransitionImageLayout(Image image, ImageLayout oldLayout, ImageLayout newLayout)
    {
        ExecuteImmediateCommand(buffer =>
        {
            var (srcStage, dstStage, srcAccess, dstAccess) = GetBarrierInfo(oldLayout, newLayout);
            uint sourceQueue = _graphicsQueueFamilyIndex == _presentQueueFamilyIndex ? Constants.QueueFamilyIgnored : _graphicsQueueFamilyIndex;
            uint destinationQueue = _graphicsQueueFamilyIndex == _presentQueueFamilyIndex ? Constants.QueueFamilyIgnored : _presentQueueFamilyIndex;
            var range = new ImageSubresourceRange(ImageAspectFlags.Color, 0, 1, 0, 1);
            buffer.PipelineBarrier(srcStage, dstStage, srcAccess, dstAccess, oldLayout, newLayout, sourceQueue, destinationQueue, image, range);
        });
    }

    private (PipelineStageFlags SrcStage, PipelineStageFlags DstStage, AccessFlags SrcAccess, AccessFlags DstAccess) GetBarrierInfo(ImageLayout oldLayout, ImageLayout newLayout)
    {
        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.ColorAttachmentOptimal)
        {
            return (PipelineStageFlags.TopOfPipe, PipelineStageFlags.ColorAttachmentOutput, AccessFlags.None, AccessFlags.ColorAttachmentWrite);
        }

        if (oldLayout == ImageLayout.ColorAttachmentOptimal && newLayout == ImageLayout.PresentSrc)
        {
            return (PipelineStageFlags.ColorAttachmentOutput, PipelineStageFlags.BottomOfPipe, AccessFlags.ColorAttachmentWrite, AccessFlags.MemoryRead);
        }

        if (oldLayout == ImageLayout.PresentSrc && newLayout == ImageLayout.ColorAttachmentOptimal)
        {
            return (PipelineStageFlags.BottomOfPipe, PipelineStageFlags.ColorAttachmentOutput, AccessFlags.MemoryRead, AccessFlags.ColorAttachmentWrite);
        }

        return (PipelineStageFlags.TopOfPipe, PipelineStageFlags.BottomOfPipe, AccessFlags.None, AccessFlags.None);
    }

    private void ExecuteImmediateCommand(Action<CommandBuffer> record)
    {
        var commandBuffers = _device!.AllocateCommandBuffers(_commandPool!, CommandBufferLevel.Primary, 1);
        var commandBuffer = commandBuffers[0];

        commandBuffer.Begin(CommandBufferUsageFlags.OneTimeSubmit);
        record(commandBuffer);
        commandBuffer.End();

        var submitInfo = new SubmitInfo
        {
            CommandBuffers = new[] { commandBuffer }
        };

        _graphicsQueue!.Submit(new[] { submitInfo }, null);
        _graphicsQueue.WaitIdle();

        _commandPool!.FreeCommandBuffers(commandBuffers);
    }

    private IntPtr GetSharpVkProc(string name, Instance instance, Device device)
    {
        if (device != null)
            return device.GetProcedureAddress(name);
        if (instance != null)
            return instance.GetProcedureAddress(name);
        return _instance!.GetProcedureAddress(name);
    }

    private void DrawFrame(SKSurface surface)
    {
        int width = (int)_swapchainExtent.Width;
        int height = (int)_swapchainExtent.Height;
        var canvas = surface.Canvas;

        canvas.Clear(new SKColor(0x33, 0x55, 0x66));

        canvas.Save();
        canvas.Translate(width / 2f, height / 2f);
        canvas.RotateDegrees((Environment.TickCount64 % 3600) / 10f);

        float radius = MathF.Min(width, height) * 0.35f;

        using var triangle = new SKPath();
        triangle.MoveTo(0, -radius);
        triangle.LineTo(-radius * 0.8660254f, radius * 0.5f);
        triangle.LineTo(radius * 0.8660254f, radius * 0.5f);
        triangle.Close();

        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, -radius),
            new SKPoint(0, radius),
            new[]
            {
                new SKColor(0xFF, 0x57, 0x22),
                new SKColor(0x29, 0xB6, 0xF6),
                new SKColor(0x66, 0xBB, 0x6A)
            },
            null,
            SKShaderTileMode.Clamp);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = shader
        };

        canvas.DrawPath(triangle, paint);
        canvas.Restore();

        using var textPaint = new SKPaint
        {
            Color = new SKColor(0xEC, 0xF0, 0xF1),
            IsAntialias = true,
            TextSize = MathF.Max(16f, MathF.Min(width, height) * 0.06f)
        };

        const string message = "Rendered with SkiaSharp Vulkan";
        float textWidth = textPaint.MeasureText(message);
        var metrics = textPaint.FontMetrics;
        float baseline = height - MathF.Max(24f, height * 0.05f) - metrics.Descent;
        float textX = (width - textWidth) / 2f;

        canvas.DrawText(message, textX, baseline, textPaint);
    }

    private struct SwapchainSupportDetails
    {
        public SurfaceCapabilities Capabilities;
        public SurfaceFormat[] Formats;
        public PresentMode[] PresentModes;
    }
}
