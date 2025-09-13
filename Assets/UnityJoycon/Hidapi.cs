#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityJoycon
{
    public class Hidapi : IDisposable
    {
        private bool _disposedValue;

        public Hidapi()
        {
            Debug.Log($"Initializing {nameof(Hidapi)}");
            var res = NativeMethods.hid_init();
            if (res != 0) throw GetError();
        }

        public void Dispose()
        {
            if (_disposedValue) return;
            Debug.Log($"Destroying {nameof(Hidapi)}");
            var res = NativeMethods.hid_exit();
            if (res != 0) throw GetError();
            _disposedValue = true;
        }

        public List<HidDeviceInfo> GetDevices(ushort? vendorId = null, ushort? productId = null)
        {
            unsafe
            {
                var ptr = NativeMethods.hid_enumerate(vendorId ?? 0, productId ?? 0);
                var firstPtr = ptr;
                if (ptr == null) return new List<HidDeviceInfo>();

                var list = new List<HidDeviceInfo>();
                while (ptr != null)
                {
                    var deviceInfo = new HidDeviceInfo(ptr);
                    list.Add(deviceInfo);
                    ptr = ptr->next;
                }

                NativeMethods.hid_free_enumeration(firstPtr);

                return list;
            }
        }

        public HidDevice OpenDevice(HidDeviceInfo info)
        {
            unsafe
            {
                var pathPtr = Marshal.StringToCoTaskMemAnsi(info.Path);
                if (pathPtr == IntPtr.Zero) throw new Exception("Failed to allocate memory for device path.");

                var device = NativeMethods.hid_open_path((byte*)pathPtr);
                Marshal.FreeCoTaskMem(pathPtr);

                if (device == null) throw GetError();

                return new HidDevice(device);
            }
        }

        private static HidapiException GetError()
        {
            unsafe
            {
                var errPtr = NativeMethods.hid_error(null);
                var message = Marshal.PtrToStringAnsi((IntPtr)errPtr) ?? "Unknown error";
                return new HidapiException(message);
            }
        }
    }

    public class HidDevice : IDisposable
    {
        private readonly unsafe hid_device_* _device;
        private bool _disposedValue;

        internal unsafe HidDevice(hid_device_* device)
        {
            _device = device;
        }

        public void Dispose()
        {
            if (_disposedValue) return;

            unsafe
            {
                NativeMethods.hid_close(_device);
            }

            _disposedValue = true;
        }

        public void SetBlockingMode(bool blocking)
        {
            unsafe
            {
                var res = NativeMethods.hid_set_nonblocking(_device, blocking ? 0 : 1);
                if (res != 0) throw GetError();
            }
        }

        public nuint Read(Span<byte> buffer)
        {
            unsafe
            {
                fixed (byte* bufPtr = buffer)
                {
                    var size = NativeMethods.hid_read(_device, bufPtr, (nuint)buffer.Length);
                    if (size < 0) throw GetReadError();

                    return (nuint)size;
                }
            }
        }

        public nuint ReadTimeout(Span<byte> buffer, TimeSpan timeout)
        {
            unsafe
            {
                fixed (byte* bufPtr = buffer)
                {
                    var size = NativeMethods.hid_read_timeout(_device, bufPtr, (nuint)buffer.Length,
                        (int)timeout.TotalMilliseconds);
                    if (size < 0) throw GetReadError();

                    return (nuint)size;
                }
            }
        }

        public void Write(ReadOnlySpan<byte> buffer)
        {
            unsafe
            {
                fixed (byte* bufPtr = buffer)
                {
                    var res = NativeMethods.hid_write(_device, bufPtr, (nuint)buffer.Length);
                    if (res < 0) throw GetError();
                }
            }
        }

        private HidapiException GetError()
        {
            unsafe
            {
                var errPtr = NativeMethods.hid_error(_device);
                var message = Marshal.PtrToStringAnsi((IntPtr)errPtr) ?? "Unknown error";
                return new HidapiException(message);
            }
        }

        private HidapiException GetReadError()
        {
            unsafe
            {
                var errPtr = NativeMethods.hid_read_error(_device);
                var message = Marshal.PtrToStringAnsi((IntPtr)errPtr) ?? "Unknown error";
                return new HidapiException(message);
            }
        }
    }

    public record HidDeviceInfo
    {
        public readonly uint BusType;
        public readonly int InterfaceNumber;
        public readonly string? ManufacturerString;
        public readonly string? Path;
        public readonly ushort ProductId;
        public readonly string? ProductString;
        public readonly ushort ReleaseNumber;
        public readonly string? SerialNumber;
        public readonly ushort Usage;
        public readonly ushort UsagePage;
        public readonly ushort VendorId;

        internal unsafe HidDeviceInfo(hid_device_info* ptr)
        {
            Path = Marshal.PtrToStringAnsi((IntPtr)ptr->path);
            VendorId = ptr->vendor_id;
            ProductId = ptr->product_id;
            SerialNumber = Marshal.PtrToStringUni((IntPtr)ptr->serial_number);
            ReleaseNumber = ptr->release_number;
            ManufacturerString = Marshal.PtrToStringUni((IntPtr)ptr->manufacturer_string);
            ProductString = Marshal.PtrToStringUni((IntPtr)ptr->product_string);
            UsagePage = ptr->usage_page;
            Usage = ptr->usage;
            InterfaceNumber = ptr->interface_number;
            BusType = ptr->bus_type;
        }
    }

    public class HidapiException : Exception
    {
        public HidapiException(string message) : base(message)
        {
        }
    }
}