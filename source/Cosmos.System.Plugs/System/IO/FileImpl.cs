﻿using global::System;
using global::System.IO;

using Cosmos.Common.Extensions;
using Cosmos.IL2CPU.Plugs;
using Cosmos.System.FileSystem;
using Cosmos.System.FileSystem.VFS;

namespace Cosmos.System.Plugs.System.IO
{
    [Plug(Target = typeof(File))]
    public static class FileImpl
    {
        

        public static bool Exists(string aFile)
        {
            return VFSManager.FileExists(aFile);
        }

        public static string ReadAllText(string aFile)
        {
            //FatHelpers.Debug("In FileImpl.ReadAllText");
            using (var xFS = new FileStream(aFile, FileMode.Open))
            {
                var xBuff = new byte[(int)xFS.Length];
                var xResult = xFS.Read(xBuff, 0, xBuff.Length);
                if (xResult != xBuff.Length)
                {
                    throw new Exception("Couldn't read complete file!");
                }
                //FatHelpers.Debug("Bytes read");
                var xResultStr = xBuff.GetUtf8String(0, (uint)xBuff.Length);
                //FatHelpers.Debug("ResultString retrieved");
                return xResultStr;
            }
        }

        public static void WriteAllText(string aFile, string aText)
        {
            using (var xFS = new FileStream(aFile, FileMode.Create))
            {
                var xBuff = aText.GetUtf8Bytes(0, (uint)aText.Length);
                xFS.Write(xBuff, 0, xBuff.Length);
            }
        }
        public static byte[] ReadAllBytes(string aFile)
        {
            //FatHelpers.Debug("In FileImpl.ReadAllText");
            using (var xFS = new FileStream(aFile, FileMode.Open))
            {
                var xBuff = new byte[(int)xFS.Length];
                var xResult = xFS.Read(xBuff, 0, xBuff.Length);
                if (xResult != xBuff.Length)
                {
                    throw new Exception("Couldn't read complete file!");
                }
                //FatHelpers.Debug("Bytes read");
                
                return xBuff;
            }
        }

        public static void WriteAllBytes(string aFile, byte[] aBytes)
        {
            using (var xFS = new FileStream(aFile, FileMode.Create))
            {
                var xBuff = aBytes;
                
                xFS.Write(xBuff, 0, xBuff.Length);
            }
        }
        public static void Copy(string srcFile, string destFile)
        {
            byte[] xBuff;
            using (var xFS = new FileStream(srcFile, FileMode.Open))
            {
                xBuff = new byte[(int)xFS.Length];
                var s1 = xFS.Read(xBuff, 0, xBuff.Length);
                var yFS = new FileStream(destFile, FileMode.Create);
                yFS.Write(xBuff, 0, xBuff.Length);

            }
        }
    }
}
