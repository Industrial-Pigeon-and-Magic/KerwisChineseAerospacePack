using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace KerwisDDSLoader
{
    /// <summary>
    /// 加载DDS文件为Texture2D.通过单例Instance调用,因为要实现缓存已加载dds的功能
    /// 作者:Bilibili @矢速Velctor
    /// </summary>
    public class DDSLoader
    {
        const int DXT10Header_ByteOffset = 124;
        const int PixelFormat_IntOffset = 18;
        const int BytesPerInt = 4;

        /// <summary>
        /// DDSLoader的单例
        /// </summary>
        public static DDSLoader Instance = new DDSLoader();

        private Dictionary<int, Texture2D> ReferenceCache;

        private DDSLoader()
        {
            ReferenceCache = new Dictionary<int, Texture2D>();
        }
        private static int GetByteArrayHashCode(byte[] array)
        {
#if DEBUG
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
#endif
            int threads = Environment.ProcessorCount;
            int[] hashs = new int[threads];
            int length = array.Length;
            int fragLength = (length / threads) + ((length % threads) == 0 ? 0 : 1);
            Parallel.For(0, threads-1, j =>
             {
                 int remain = length - (fragLength * j);
                 for(int i = fragLength*j;i< (remain<fragLength?remain:fragLength);i++)
                 hashs[j] = hashs[j] * 31 + array[i];
             });
            int hash = 0;
            foreach (int i in hashs)
                hash = hash * 31 + i;
#if DEBUG
            sw.Stop();
            Debug.Log("hash time:" + sw.ElapsedMilliseconds+"ms.");
#endif
            return hash;
        }
        /// <summary>
        /// 从指定的位置加载一张dds为UnityEngine.Texture2D,编码将自动识别(未经所有dds种类验证)
        /// </summary>
        /// <param name="DDSPath">dds的完整目录</param>
        /// <returns>获得的纹理.如果失败则返回空</returns>
        public Texture2D FromFile(string DDSPath)
        {
            //加载所有字节
            byte[] DDSBytes;
            string ddsName = Path.GetFileName(DDSPath);
            try
            {
                DDSBytes = File.ReadAllBytes(DDSPath);
            }
            catch(Exception e)
            {
                Debug.LogError("DDSLoader:Error while loading " + ddsName + ":\n" + e.GetType() + "\n:" + e.Message);
                return null;
            }
            //如果加载过了,就直接返回之前加载的
            int DDSHash = GetByteArrayHashCode(DDSBytes);
#if DEBUG
            Debug.Log("DDSLoader:Loading" + DDSPath+"\nHashCode:"+DDSHash.ToString("X"));
#endif
            if(ReferenceCache.ContainsKey(DDSHash)) return ReferenceCache[DDSHash];

            GCHandle ddsHandle = GCHandle.Alloc(DDSBytes, GCHandleType.Pinned);
            IntPtr Address = ddsHandle.AddrOfPinnedObject();
            if (Marshal.ReadInt32(Address) != 0x20534444)
            {
                Debug.LogError("DDSLoader:" + ddsName + " is not a dds file!(dwMagic is not \"DDS \")");
                ddsHandle.Free();
                return null;
            }
            int width = Marshal.ReadInt32(Address, BytesPerInt * 4);
            int height = Marshal.ReadInt32(Address, BytesPerInt * 3);
            int mipchain = Marshal.ReadInt32(Address, BytesPerInt * 7);
            int FourCC = Marshal.ReadInt32(Address, BytesPerInt * (PixelFormat_IntOffset + 3));
            int headerSize = FourCC == FC2I("DX10") ? 148 : 128;
            GraphicsFormat unityGraphicFormat = GetDDSFormat(Address);
            TextureCreationFlags creationFlags;
            if (mipchain == 0) creationFlags = TextureCreationFlags.None;
            else creationFlags = TextureCreationFlags.MipChain;
            Texture2D unitytex = new Texture2D(width, height, unityGraphicFormat, creationFlags);
            unitytex.name = Path.GetFileNameWithoutExtension(ddsName);
            unitytex.LoadRawTextureData(Address + headerSize, DDSBytes.Length - headerSize);
            unitytex.Apply();
            ddsHandle.Free();
            //把加载好的Texture2D加入缓存
            ReferenceCache.Add(DDSHash, unitytex);
            return unitytex;
        }
        /// <summary>
        /// 从指定的位置以指定TextureFormat加载一张dds为UnityEngine.Texture2D
        /// </summary>
        /// <param name="DDSPath">dds的完整目录</param>
        /// <param name="textureFormat">自行指定的TextureFormat.如果与指定dds实际编码不符,将导致错误</param>
        /// <returns>获得的纹理.如果失败则返回空</returns>
        public Texture2D FromFile(string DDSPath,TextureFormat textureFormat)
        {
            byte[] DDSBytes;
            string ddsName = Path.GetFileName(DDSPath);
            try
            {
                DDSBytes = File.ReadAllBytes(DDSPath);
            }
            catch (Exception e)
            {
                Debug.LogError("DDSLoader:Error while loading " + ddsName + ":\n" + e.GetType() + "\n:" + e.Message);
                return null;
            }
            //如果加载过了,就直接返回之前加载的
            int DDSHash = GetByteArrayHashCode(DDSBytes);
            if (ReferenceCache.ContainsKey(DDSHash)) return ReferenceCache[DDSHash];

            GCHandle ddsHandle = GCHandle.Alloc(DDSBytes, GCHandleType.Pinned);
            IntPtr Address = ddsHandle.AddrOfPinnedObject();
            if (Marshal.ReadInt32(Address) != 0x20534444)
            {
                Debug.LogError("DDSLoader:" + ddsName + " is not a dds file!(dwMagic is not \"DDS \")");
                ddsHandle.Free();
                return null;
            }
            int width = Marshal.ReadInt32(Address, BytesPerInt * 4);
            int height = Marshal.ReadInt32(Address, BytesPerInt * 3);
            int mipchain = Marshal.ReadInt32(Address, BytesPerInt * 7);
            int FourCC = Marshal.ReadInt32(Address, BytesPerInt * (PixelFormat_IntOffset + 3));
            int headerSize = FourCC == FC2I("DX10") ? 148 : 128;
            Texture2D unitytex = new Texture2D(width, height, textureFormat, mipchain == 0);
            unitytex.name = Path.GetFileNameWithoutExtension(ddsName);
            unitytex.LoadRawTextureData(Address + headerSize, DDSBytes.Length - headerSize);
            unitytex.Apply();
            ddsHandle.Free();
            //把加载好的Texture2D加入缓存
            ReferenceCache.Add(DDSHash, unitytex);
            return unitytex;
        }
        /// <summary>
        /// 从指定的位置以指定GraphicsFormat,TextureCreationFlags加载一张dds为UnityEngine.Texture2D
        /// </summary>
        /// <param name="DDSPath">dds的完整目录</param>
        /// <param name="graphicsFormat">自行指定的GraphicsFormat.如果与指定dds实际编码不符,将导致错误</param>
        /// <param name="flags">自行指定TextureCreationFlags.如果与指定dds实际不符,将可能导致错误</param>
        /// <returns></returns>
        public Texture2D FromFile(string DDSPath, GraphicsFormat graphicsFormat, TextureCreationFlags flags)
        {
            byte[] DDSBytes;
            string ddsName = Path.GetFileName(DDSPath);
            try
            {
                DDSBytes = File.ReadAllBytes(DDSPath);
            }
            catch (Exception e)
            {
                Debug.LogError("DDSLoader:Error while loading " + ddsName + ":\n" + e.GetType() + "\n:" + e.Message);
                return null;
            }
            //如果加载过了,就直接返回之前加载的
            int DDSHash = GetByteArrayHashCode(DDSBytes);
            if (ReferenceCache.ContainsKey(DDSHash)) return ReferenceCache[DDSHash];

            GCHandle ddsHandle = GCHandle.Alloc(DDSBytes, GCHandleType.Pinned);
            IntPtr Address = ddsHandle.AddrOfPinnedObject();
            if (Marshal.ReadInt32(Address) != 0x20534444)
            {
                Debug.LogError("DDSLoader:" + ddsName + " is not a dds file!(dwMagic is not \"DDS \")");
                ddsHandle.Free();
                return null;
            }
            int width = Marshal.ReadInt32(Address, BytesPerInt * 4);
            int height = Marshal.ReadInt32(Address, BytesPerInt * 3);
            int mipchain = Marshal.ReadInt32(Address, BytesPerInt * 7);
            int FourCC = Marshal.ReadInt32(Address, BytesPerInt * (PixelFormat_IntOffset + 3));
            int headerSize = FourCC == FC2I("DX10") ? 148 : 128;
            Texture2D unitytex = new Texture2D(width, height, graphicsFormat, flags);
            unitytex.name = Path.GetFileNameWithoutExtension(ddsName);
            unitytex.LoadRawTextureData(Address + headerSize, DDSBytes.Length - headerSize);
            unitytex.Apply();
            ddsHandle.Free();
            //把加载好的Texture2D加入缓存
            ReferenceCache.Add(DDSHash, unitytex);
            return unitytex;
        }
        static int FC2I(string fourChar) => fourChar[0] | (fourChar[1] << 8) | (fourChar[2] << 16) | (fourChar[3] << 24);//four char to integer
        static GraphicsFormat GetDDSFormat(IntPtr DDSAddress)
        {
            int Flags = Marshal.ReadInt32(DDSAddress, BytesPerInt * (PixelFormat_IntOffset + 2));
            bool DDPF_FOURCC = (Flags & 4) == 4;
            int FourCC = Marshal.ReadInt32(DDSAddress, BytesPerInt * (PixelFormat_IntOffset + 3));
            if (!(DDPF_FOURCC || FourCC != 0))
                Debug.Log("这个分支将永远不会在正常的dds格式下被运行." +
                    "这个插件专用于Kerwis的mod,仅在我们定制的美术流程范围内被验证:" +
                    "Nvidia Texture Tool 2021-BC1,BC3,BC4,BC5,BC6H,BC7" +
                    "如果你计划使用这个程序作其它用途,请自行验证并完善GraphicsFormat识别部分.");
            if (DDPF_FOURCC)
            {
                switch(FourCC)
                {
                    case 'D' | ('X' << 8) | ('1' << 16) | ('0' << 24): break;
                    case 'D' | ('X' << 8) | ('T' << 16) | ('1' << 24): return GraphicsFormat.RGBA_DXT1_UNorm;
                    case 'D' | ('X' << 8) | ('T' << 16) | ('3' << 24): return GraphicsFormat.RGBA_DXT3_UNorm;
                    case 'D' | ('X' << 8) | ('T' << 16) | ('5' << 24): return GraphicsFormat.RGBA_DXT5_UNorm;
                    case 'B' | ('C' << 8) | ('4' << 16) | ('U' << 24): return GraphicsFormat.R_BC4_UNorm;
                    case 'B' | ('C' << 8) | ('4' << 16) | ('S' << 24): return GraphicsFormat.R_BC4_SNorm;
                    case 'A' | ('T' << 8) | ('I' << 16) | ('2' << 24): return GraphicsFormat.RG_BC5_UNorm;
                    case 'B' | ('C' << 8) | ('5' << 16) | ('S' << 24): return GraphicsFormat.RG_BC5_SNorm;
                    case 'R' | ('G' << 8) | ('B' << 16) | ('G' << 24): return GraphicsFormat.R8G8B8A8_UNorm;
                    case 'G' | ('R' << 8) | ('G' << 16) | ('B' << 24): return GraphicsFormat.R8G8B8A8_UNorm;
                    case 36: return GraphicsFormat.R16G16B16A16_UNorm;
                    case 110:return GraphicsFormat.R16G16B16A16_SNorm;
                    case 111:return GraphicsFormat.R16_SFloat;
                    case 112:return GraphicsFormat.R16G16_SFloat;
                    case 113:return GraphicsFormat.R16G16B16A16_SFloat;
                    case 114:return GraphicsFormat.R32_SFloat;
                    case 115:return GraphicsFormat.R32G32_SFloat;
                    case 116:return GraphicsFormat.R32G32B32A32_SFloat;
                    case 'D' | ('X' << 8) | ('T' << 16) | ('2' << 24): throw new DDSFormatException("DXT2 no longer supported by Unity.");
                    case 'D' | ('X' << 8) | ('T' << 16) | ('4' << 24): throw new DDSFormatException("DXT4 no longer supported by Unity.");
                    case 'U' | ('Y' << 8) | ('V' << 16) | ('Y' << 24): throw new DDSFormatException("UYVY no longer supported by Unity.");
                    case 'Y' | ('U' << 8) | ('Y' << 16) | ('2' << 24): throw new DDSFormatException("YUY2 no longer supported by Unity.");
                    case 117: throw new DDSFormatException("CxV8U8 no longer supported by Unity.");
                    default: throw new DDSFormatException("Unknown FourCC:0x"+FourCC.ToString("X"));
                }
                switch((DXGI_FORMAT)Marshal.ReadInt32(DDSAddress, DXT10Header_ByteOffset + BytesPerInt)) //header Size+dw Magic Size
                {
                    case DXGI_FORMAT.DXGI_FORMAT_BC1_TYPELESS:return GraphicsFormat.RGBA_DXT1_UNorm;
                    case DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM: return GraphicsFormat.RGBA_DXT1_UNorm;
                    case DXGI_FORMAT.DXGI_FORMAT_BC1_UNORM_SRGB: return GraphicsFormat.RGBA_DXT1_SRGB;
                    case DXGI_FORMAT.DXGI_FORMAT_BC2_TYPELESS: return GraphicsFormat.RGBA_DXT3_UNorm;
                    case DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM: return GraphicsFormat.RGBA_DXT3_UNorm;
                    case DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM_SRGB: return GraphicsFormat.RGBA_DXT3_SRGB;
                    case DXGI_FORMAT.DXGI_FORMAT_BC3_TYPELESS: return GraphicsFormat.RGBA_DXT5_UNorm;
                    case DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM: return GraphicsFormat.RGBA_DXT5_UNorm;
                    case DXGI_FORMAT.DXGI_FORMAT_BC3_UNORM_SRGB: return GraphicsFormat.RGBA_DXT5_SRGB;
                    case DXGI_FORMAT.DXGI_FORMAT_BC4_TYPELESS: return GraphicsFormat.R_BC4_UNorm;
                    case DXGI_FORMAT.DXGI_FORMAT_BC4_SNORM : return GraphicsFormat.R_BC4_SNorm;
                    case DXGI_FORMAT.DXGI_FORMAT_BC4_UNORM: return GraphicsFormat.R_BC4_UNorm;
                    case DXGI_FORMAT.DXGI_FORMAT_BC5_TYPELESS: return GraphicsFormat.RG_BC5_UNorm;
                    case DXGI_FORMAT.DXGI_FORMAT_BC5_SNORM: return GraphicsFormat.RG_BC5_SNorm;
                    case DXGI_FORMAT.DXGI_FORMAT_BC5_UNORM: return GraphicsFormat.RG_BC5_UNorm;
                    case DXGI_FORMAT.DXGI_FORMAT_BC6H_TYPELESS: return GraphicsFormat.RGB_BC6H_SFloat;
                    case DXGI_FORMAT.DXGI_FORMAT_BC6H_UF16: return GraphicsFormat.RGB_BC6H_UFloat;
                    case DXGI_FORMAT.DXGI_FORMAT_BC6H_SF16: return GraphicsFormat.RGB_BC6H_SFloat;
                    case DXGI_FORMAT.DXGI_FORMAT_BC7_TYPELESS: return GraphicsFormat.RGBA_BC7_UNorm;
                    case DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM: return GraphicsFormat.RGBA_BC7_UNorm;
                    case DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM_SRGB:return GraphicsFormat.RGBA_BC7_SRGB;
                    default: return GraphicsFormat.None;
                }
            }
            else
            {
                bool DDPF_ALPHAPIXELS = (Flags & 1) == 1;
                bool DDPF_RGB = (Flags & 0x40) == 0x40;
                int dwRGBBitCount = Marshal.ReadInt32(DDSAddress, BytesPerInt * (PixelFormat_IntOffset + 4));
                switch (dwRGBBitCount)
                {
                    case 8: return GraphicsFormat.R8_UNorm;
                    case 16: return GraphicsFormat.R16_SFloat;
                    case 24: return GraphicsFormat.R8G8_UNorm;
                    case 32:
                        if (DDPF_ALPHAPIXELS) return GraphicsFormat.B8G8R8A8_UNorm;
                        else return GraphicsFormat.R16G16_SFloat;
                    case 48: return GraphicsFormat.R16G16B16_SFloat;
                    case 64:
                        if (DDPF_ALPHAPIXELS) return GraphicsFormat.R16G16B16A16_SFloat;
                        else return GraphicsFormat.R32G32_SFloat;
                    case 128: return GraphicsFormat.R32G32B32A32_SFloat;
                    default: throw new DDSFormatException("uncompressed dds with unsupported RGBBitCount:" + dwRGBBitCount);
                }
            }
        }
    }
    class DDSFormatException : IOException
    {
        public DDSFormatException(string message) : base(message)
        {
        }
    }
    enum DXGI_FORMAT
    {
        DXGI_FORMAT_UNKNOWN,
        DXGI_FORMAT_R32G32B32A32_TYPELESS,
        DXGI_FORMAT_R32G32B32A32_FLOAT,
        DXGI_FORMAT_R32G32B32A32_UINT,
        DXGI_FORMAT_R32G32B32A32_SINT,
        DXGI_FORMAT_R32G32B32_TYPELESS,
        DXGI_FORMAT_R32G32B32_FLOAT,
        DXGI_FORMAT_R32G32B32_UINT,
        DXGI_FORMAT_R32G32B32_SINT,
        DXGI_FORMAT_R16G16B16A16_TYPELESS,
        DXGI_FORMAT_R16G16B16A16_FLOAT,
        DXGI_FORMAT_R16G16B16A16_UNORM,
        DXGI_FORMAT_R16G16B16A16_UINT,
        DXGI_FORMAT_R16G16B16A16_SNORM,
        DXGI_FORMAT_R16G16B16A16_SINT,
        DXGI_FORMAT_R32G32_TYPELESS,
        DXGI_FORMAT_R32G32_FLOAT,
        DXGI_FORMAT_R32G32_UINT,
        DXGI_FORMAT_R32G32_SINT,
        DXGI_FORMAT_R32G8X24_TYPELESS,
        DXGI_FORMAT_D32_FLOAT_S8X24_UINT,
        DXGI_FORMAT_R32_FLOAT_X8X24_TYPELESS,
        DXGI_FORMAT_X32_TYPELESS_G8X24_UINT,
        DXGI_FORMAT_R10G10B10A2_TYPELESS,
        DXGI_FORMAT_R10G10B10A2_UNORM,
        DXGI_FORMAT_R10G10B10A2_UINT,
        DXGI_FORMAT_R11G11B10_FLOAT,
        DXGI_FORMAT_R8G8B8A8_TYPELESS,
        DXGI_FORMAT_R8G8B8A8_UNORM,
        DXGI_FORMAT_R8G8B8A8_UNORM_SRGB,
        DXGI_FORMAT_R8G8B8A8_UINT,
        DXGI_FORMAT_R8G8B8A8_SNORM,
        DXGI_FORMAT_R8G8B8A8_SINT,
        DXGI_FORMAT_R16G16_TYPELESS,
        DXGI_FORMAT_R16G16_FLOAT,
        DXGI_FORMAT_R16G16_UNORM,
        DXGI_FORMAT_R16G16_UINT,
        DXGI_FORMAT_R16G16_SNORM,
        DXGI_FORMAT_R16G16_SINT,
        DXGI_FORMAT_R32_TYPELESS,
        DXGI_FORMAT_D32_FLOAT,
        DXGI_FORMAT_R32_FLOAT,
        DXGI_FORMAT_R32_UINT,
        DXGI_FORMAT_R32_SINT,
        DXGI_FORMAT_R24G8_TYPELESS,
        DXGI_FORMAT_D24_UNORM_S8_UINT,
        DXGI_FORMAT_R24_UNORM_X8_TYPELESS,
        DXGI_FORMAT_X24_TYPELESS_G8_UINT,
        DXGI_FORMAT_R8G8_TYPELESS,
        DXGI_FORMAT_R8G8_UNORM,
        DXGI_FORMAT_R8G8_UINT,
        DXGI_FORMAT_R8G8_SNORM,
        DXGI_FORMAT_R8G8_SINT,
        DXGI_FORMAT_R16_TYPELESS,
        DXGI_FORMAT_R16_FLOAT,
        DXGI_FORMAT_D16_UNORM,
        DXGI_FORMAT_R16_UNORM,
        DXGI_FORMAT_R16_UINT,
        DXGI_FORMAT_R16_SNORM,
        DXGI_FORMAT_R16_SINT,
        DXGI_FORMAT_R8_TYPELESS,
        DXGI_FORMAT_R8_UNORM,
        DXGI_FORMAT_R8_UINT,
        DXGI_FORMAT_R8_SNORM,
        DXGI_FORMAT_R8_SINT,
        DXGI_FORMAT_A8_UNORM,
        DXGI_FORMAT_R1_UNORM,
        DXGI_FORMAT_R9G9B9E5_SHAREDEXP,
        DXGI_FORMAT_R8G8_B8G8_UNORM,
        DXGI_FORMAT_G8R8_G8B8_UNORM,
        DXGI_FORMAT_BC1_TYPELESS,
        DXGI_FORMAT_BC1_UNORM,
        DXGI_FORMAT_BC1_UNORM_SRGB,
        DXGI_FORMAT_BC2_TYPELESS,
        DXGI_FORMAT_BC2_UNORM,
        DXGI_FORMAT_BC2_UNORM_SRGB,
        DXGI_FORMAT_BC3_TYPELESS,
        DXGI_FORMAT_BC3_UNORM,
        DXGI_FORMAT_BC3_UNORM_SRGB,
        DXGI_FORMAT_BC4_TYPELESS,
        DXGI_FORMAT_BC4_UNORM,
        DXGI_FORMAT_BC4_SNORM,
        DXGI_FORMAT_BC5_TYPELESS,
        DXGI_FORMAT_BC5_UNORM,
        DXGI_FORMAT_BC5_SNORM,
        DXGI_FORMAT_B5G6R5_UNORM,
        DXGI_FORMAT_B5G5R5A1_UNORM,
        DXGI_FORMAT_B8G8R8A8_UNORM,
        DXGI_FORMAT_B8G8R8X8_UNORM,
        DXGI_FORMAT_R10G10B10_XR_BIAS_A2_UNORM,
        DXGI_FORMAT_B8G8R8A8_TYPELESS,
        DXGI_FORMAT_B8G8R8A8_UNORM_SRGB,
        DXGI_FORMAT_B8G8R8X8_TYPELESS,
        DXGI_FORMAT_B8G8R8X8_UNORM_SRGB,
        DXGI_FORMAT_BC6H_TYPELESS,
        DXGI_FORMAT_BC6H_UF16,
        DXGI_FORMAT_BC6H_SF16,
        DXGI_FORMAT_BC7_TYPELESS,
        DXGI_FORMAT_BC7_UNORM,
        DXGI_FORMAT_BC7_UNORM_SRGB,
        DXGI_FORMAT_AYUV,
        DXGI_FORMAT_Y410,
        DXGI_FORMAT_Y416,
        DXGI_FORMAT_NV12,
        DXGI_FORMAT_P010,
        DXGI_FORMAT_P016,
        DXGI_FORMAT_420_OPAQUE,
        DXGI_FORMAT_YUY2,
        DXGI_FORMAT_Y210,
        DXGI_FORMAT_Y216,
        DXGI_FORMAT_NV11,
        DXGI_FORMAT_AI44,
        DXGI_FORMAT_IA44,
        DXGI_FORMAT_P8,
        DXGI_FORMAT_A8P8,
        DXGI_FORMAT_B4G4R4A4_UNORM,
        DXGI_FORMAT_P208,
        DXGI_FORMAT_V208,
        DXGI_FORMAT_V408,
        DXGI_FORMAT_SAMPLER_FEEDBACK_MIN_MIP_OPAQUE,
        DXGI_FORMAT_SAMPLER_FEEDBACK_MIP_REGION_USED_OPAQUE,
        DXGI_FORMAT_FORCE_UINT
    }
}
