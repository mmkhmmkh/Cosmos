﻿using Cosmos.Common.Extensions;
using Cosmos.System.FileSystem.Listing;

using global::System;
using global::System.Collections.Generic;

namespace Cosmos.System.FileSystem.FAT.Listing
{
    using global::System.IO;

    internal class FatDirectoryEntry : DirectoryEntry
    {
        private readonly uint mEntryHeaderDataOffset;

        private new readonly FatFileSystem mFileSystem;

        private readonly ulong mFirstClusterNum;

        private new readonly FatDirectoryEntry mParent;

        // Size is UInt32 because FAT doesn't support bigger.
        // Don't change to UInt64
        public FatDirectoryEntry(
            FatFileSystem aFileSystem,
            FatDirectoryEntry aParent,
            string aName,
            uint aSize,
            ulong aFirstCluster,
            uint aEntryHeaderDataOffset,
            DirectoryEntryTypeEnum aEntryType)
            : base(aFileSystem, aParent, aName, aSize, aEntryType)
        {
            if (aFileSystem == null)
            {
                throw new ArgumentNullException("aFileSystem");
            }

            if (aName == null)
            {
                throw new ArgumentNullException("aName");
            }

            if (aFirstCluster < 2)
            {
                throw new ArgumentOutOfRangeException("aFirstCluster");
            }

            mFileSystem = aFileSystem;
            mParent = aParent;
            mFirstClusterNum = aFirstCluster;
            mEntryHeaderDataOffset = aEntryHeaderDataOffset;

            //FatHelpers.Debug(
            //    "-- FatDirectoryEntry.ctor : " + "aParent.Name = " + aParent?.mName + ", aName = " + aName + ", aSize = "
            //    + aSize + ", aFirstCluster = " + aFirstCluster + ", aEntryHeaderDataOffset = " + aEntryHeaderDataOffset
            //    + " --");
        }

        public FatDirectoryEntry(
            FatFileSystem aFileSystem,
            FatDirectoryEntry aParent,
            string aName,
            ulong aFirstCluster)
            : base(aFileSystem, aParent, aName, 0, DirectoryEntryTypeEnum.Directory)
        {
            if (aFileSystem == null)
            {
                throw new ArgumentNullException("aFileSystem");
            }

            if (aName == null)
            {
                throw new ArgumentNullException("aName");
            }

            if (aFirstCluster < 2)
            {
                throw new ArgumentOutOfRangeException("aFirstCluster");
            }

            mFileSystem = aFileSystem;
            mParent = aParent;
            mFirstClusterNum = aFirstCluster;
            mEntryHeaderDataOffset = 0;

            //FatHelpers.Debug(
            //    "-- FatDirectoryEntry.ctor : " + "aParent.Name = " + aParent?.mName + ", aName = " + aName
            //    + ", aFirstCluster = " + aFirstCluster + " --");
        }

        public ulong[] GetFatTable()
        {
            return null;
        }

        public FatFileSystem GetFileSystem()
        {
            return mFileSystem;
        }

        public override Stream GetFileStream()
        {
            if (mEntryType == DirectoryEntryTypeEnum.File)
            {
                return new FatStream(this);
            }

            return null;
        }

        public override void SetName(string aName)
        {
            SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.ShortName, aName);
        }

        public override void SetSize(long aSize)
        {
            SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.Size, (uint)aSize);
        }

        private void AllocateDirectoryEntry()
        {
            // TODO: Deal with short and long name.
            SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.ShortName, mName);
            SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.Attributes, FatDirectoryEntryAttributeConsts.Directory);
            SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.FirstClusterHigh, (uint)(mFirstClusterNum >> 16));
            SetDirectoryEntryMetadataValue(FatDirectoryEntryMetadata.FirstClusterLow, (uint)(mFirstClusterNum & 0xFFFF));
            byte[] xData = GetDirectoryEntryData();
            SetDirectoryEntryData(xData);
        }

        public FatDirectoryEntry AddDirectoryEntry(string aName, DirectoryEntryTypeEnum aType)
        {
            if (aType == DirectoryEntryTypeEnum.Directory)
            {
                uint xFirstCluster = mFileSystem.GetFat(0).GetNextUnallocatedFatEntry();
                uint xEntryHeaderDataOffset = GetNextUnallocatedEntry();
                var xNewEntry = new FatDirectoryEntry(
                    mFileSystem,
                    this,
                    aName,
                    0,
                    xFirstCluster,
                    xEntryHeaderDataOffset,
                    aType);
                xNewEntry.AllocateDirectoryEntry();
                return xNewEntry;
            }
            if (aType == DirectoryEntryTypeEnum.File)
            {
                throw new NotImplementedException("Creating new files is currently not implemented.");
            }
            throw new ArgumentOutOfRangeException("aType", "Unknown directory entry type.");
        }

        public List<FatDirectoryEntry> ReadDirectoryContents()
        {
            var xData = GetDirectoryEntryData();
            FatDirectoryEntry xParent = mParent;
            var xResult = new List<FatDirectoryEntry>();

            //TODO: Change xLongName to StringBuilder
            string xLongName = "";
            string xName = "";
            for (uint i = 0; i < xData.Length; i = i + 32)
            {
                //FatHelpers.Debug("-------------------------------------------------");
                byte xAttrib = xData[i + 11];
                byte xStatus = xData[i];

                //FatHelpers.Debug("Attrib = " + xAttrib + ", Status = " + xStatus);
                if (xAttrib == FatDirectoryEntryAttributeConsts.LongName)
                {
                    byte xType = xData[i + 12];
                    byte xOrd = xData[i];
                    //FatHelpers.Debug("Reading LFN with Seqnr " + xOrd + ", Type = " + xType);
                    if (xOrd == 0xE5)
                    {
                        FatHelpers.Debug("<DELETED>");
                        continue;
                    }
                    if (xType == 0)
                    {
                        if ((xOrd & 0x40) > 0)
                        {
                            xLongName = "";
                        }
                        //TODO: Check LDIR_Ord for ordering and throw exception
                        // if entries are found out of order.
                        // Also save buffer and only copy name if a end Ord marker is found.
                        string xLongPart = xData.GetUtf16String(i + 1, 5);
                        // We have to check the length because 0xFFFF is a valid Unicode codepoint.
                        // So we only want to stop if the 0xFFFF is AFTER a 0x0000. We can determin
                        // this by also looking at the length. Since we short circuit the or, the length
                        // is rarely evaluated.
                        if (xData.ToUInt16(i + 14) != 0xFFFF || xLongPart.Length == 5)
                        {
                            xLongPart = xLongPart + xData.GetUtf16String(i + 14, 6);
                            if (xData.ToUInt16(i + 28) != 0xFFFF || xLongPart.Length == 11)
                            {
                                xLongPart = xLongPart + xData.GetUtf16String(i + 28, 2);
                            }
                        }
                        xLongName = xLongPart + xLongName;
                        xLongPart = null;
                        //TODO: LDIR_Chksum
                    }
                }
                else
                {
                    xName = xLongName;
                    if (xStatus == 0x00)
                    {
                        FatHelpers.Debug("<EOF>");
                        break;
                    }
                    switch (xStatus)
                    {
                        case 0x05:
                            // Japanese characters - We dont handle these
                            break;
                        case 0xE5:
                            // Empty slot, skip it
                            break;
                        default:
                            if (xStatus >= 0x20)
                            {
                                if (xLongName.Length > 0)
                                {
                                    // Leading and trailing spaces are to be ignored according to spec.
                                    // Many programs (including Windows) pad trailing spaces although it
                                    // it is not required for long names.
                                    // As per spec, ignore trailing periods
                                    xName = xLongName.Trim();

                                    //If there are trailing periods
                                    int nameIndex = xName.Length - 1;
                                    if (xName[nameIndex] == '.')
                                    {
                                        //Search backwards till we find the first non-period character
                                        for (; nameIndex > 0; nameIndex--)
                                        {
                                            if (xName[nameIndex] != '.')
                                            {
                                                break;
                                            }
                                        }
                                        //Substring to remove the periods
                                        xName = xName.Substring(0, nameIndex + 1);
                                    }
                                    xLongName = "";
                                }
                                else
                                {
                                    string xEntry = xData.GetAsciiString(i, 11);
                                    xName = xEntry.Substring(0, 8).TrimEnd();
                                    string xExt = xEntry.Substring(8, 3).TrimEnd();
                                    if (xExt.Length > 0)
                                    {
                                        xName = xName + "." + xExt;
                                    }
                                }
                            }
                            break;
                    }
                }
                uint xFirstCluster = (uint)(xData.ToUInt16(i + 20) << 16 | xData.ToUInt16(i + 26));

                int xTest = xAttrib & (FatDirectoryEntryAttributeConsts.Directory | FatDirectoryEntryAttributeConsts.VolumeID);
                if (xAttrib == FatDirectoryEntryAttributeConsts.LongName)
                {
                    // skip adding, as it's a LongFileName entry, meaning the next normal entry is the item with the name.
                    //FatHelpers.Debug("Entry was a Long FileName entry. Current LongName = '" + xLongName + "'");
                }
                else if (xTest == 0)
                {
                    uint xSize = xData.ToUInt32(i + 28);
                    if (xSize == 0 && xName.Length == 0)
                    {
                        continue;
                    }
                    var xEntry = new FatDirectoryEntry(mFileSystem, xParent, xName, xSize, xFirstCluster, i, DirectoryEntryTypeEnum.File);
                    xResult.Add(xEntry);
                    FatHelpers.Debug(xEntry.mName + " --- " + xEntry.mSize + " bytes");
                }
                else if (xTest == FatDirectoryEntryAttributeConsts.Directory)
                {
                    uint xSize = xData.ToUInt32(i + 28);
                    var xEntry = new FatDirectoryEntry(mFileSystem, xParent, xName, xSize, xFirstCluster, i, DirectoryEntryTypeEnum.Directory);
                    FatHelpers.Debug(xEntry.mName + " <DIR> " + xEntry.mSize + " bytes");
                    xResult.Add(xEntry);
                }
                else if (xTest == FatDirectoryEntryAttributeConsts.VolumeID)
                {
                    FatHelpers.Debug("<VOLUME ID>");
                }
                else
                {
                    FatHelpers.Debug("<INVALID ENTRY>");
                }
            }

            return xResult;
        }

        private uint GetNextUnallocatedEntry()
        {
            var xData = GetDirectoryEntryData();
            FatHelpers.DebugNumber((uint)xData.Length);
            for (uint i = 0; i < xData.Length; i += 32)
            {
                uint x1 = xData.ToUInt32(i);
                uint x2 = xData.ToUInt32(i + 8);
                uint x3 = xData.ToUInt32(i + 16);
                uint x4 = xData.ToUInt32(i + 24);
                if ((x1 == 0) && (x2 == 0) && (x3 == 0) && (x4 == 0))
                {
                    //FatHelpers.Debug("Found unallocated Directory entry: " + i);
                    return i;
                }
            }

            // TODO: What should we return if no available entry is found.
            throw new Exception("Failed to find an unallocated directory entry.");
        }

        private byte[] GetDirectoryEntryData()
        {
            if (mEntryType != DirectoryEntryTypeEnum.Unknown)
            {
                byte[] xData;
                mFileSystem.Read(mFirstClusterNum, out xData);
                return xData;
            }

            throw new Exception("Invalid directory entry type");
        }

        private void SetDirectoryEntryData(byte[] aData)
        {
            if (aData == null)
            {
                throw new ArgumentNullException("aData");
            }

            if (aData.Length == 0)
            {
                //FatHelpers.Debug("SetDirectoryEntryData: No data to write.");
                return;
            }

            //FatHelpers.Debug("SetDirectoryEntryData: Name = " + mName);
            //FatHelpers.Debug("SetDirectoryEntryData: Size = " + mSize);
            //FatHelpers.Debug("SetDirectoryEntryData: FirstClusterNum = " + mFirstClusterNum);
            //FatHelpers.Debug("SetDirectoryEntryData: aData.Length = " + aData.Length);

            if (mEntryType != DirectoryEntryTypeEnum.Unknown)
            {
                mFileSystem.Write(mFirstClusterNum, aData);
            }
            else
            {
                throw new Exception("Invalid directory entry type");
            }
        }

        internal void SetDirectoryEntryMetadataValue(
            FatDirectoryEntryMetadata aEntryMetadata,
            uint aValue)
        {
            var xData = mParent.GetDirectoryEntryData();
            if (xData.Length > 0)
            {
                var xValue = new byte[aEntryMetadata.DataLength];
                xValue.SetUInt32(0, aValue);

                uint offset = mEntryHeaderDataOffset + aEntryMetadata.DataOffset;

                Array.Copy(xValue, 0, xData, offset, aEntryMetadata.DataLength);

                //FatHelpers.Debug("SetDirectoryEntryMetadataValue: DataLength = " + aEntryMetadata.DataLength);
                //FatHelpers.Debug("SetDirectoryEntryMetadataValue: DataOffset = " + aEntryMetadata.DataOffset);
                //FatHelpers.Debug("SetDirectoryEntryMetadataValue: EntryHeaderDataOffset = " + mEntryHeaderDataOffset);
                //FatHelpers.Debug("SetDirectoryEntryMetadataValue: TotalOffset = " + offset);
                //FatHelpers.Debug("SetDirectoryEntryMetadataValue: aValue = " + aValue);

                //for (int i = 0; i < xValue.Length; i++)
                //{
                //    FatHelpers.DebugNumber(xValue[i]);
                //}
            }

            mParent.SetDirectoryEntryData(xData);
        }

        internal void SetDirectoryEntryMetadataValue(
            FatDirectoryEntryMetadata aEntryMetadata,
            string aValue)
        {
            var xData = mParent.GetDirectoryEntryData();
            if (xData.Length > 0)
            {
                var xValue = new byte[aEntryMetadata.DataLength];
                xValue = aValue.GetUtf8Bytes(0, aEntryMetadata.DataLength);

                uint offset = mEntryHeaderDataOffset + aEntryMetadata.DataOffset;

                Array.Copy(xValue, 0, xData, offset, aEntryMetadata.DataLength);

                //FatHelpers.Debug("SetDirectoryEntryMetadataValue: DataLength = " + aEntryMetadata.DataLength);
                //FatHelpers.Debug("SetDirectoryEntryMetadataValue: DataOffset = " + aEntryMetadata.DataOffset);
                //FatHelpers.Debug("SetDirectoryEntryMetadataValue: EntryHeaderDataOffset = " + mEntryHeaderDataOffset);
                //FatHelpers.Debug("SetDirectoryEntryMetadataValue: TotalOffset = " + offset);
                //FatHelpers.Debug("SetDirectoryEntryMetadataValue: aValue = " + aValue);

                //for (int i = 0; i < xValue.Length; i++)
                //{
                //    FatHelpers.DebugNumber(xValue[i]);
                //}

                mParent.SetDirectoryEntryData(xData);
            }
        }
    }
}