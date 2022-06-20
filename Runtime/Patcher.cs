//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
using EP.U3D.LIBRARY.BASE;
using EP.U3D.LIBRARY.ASSET;
using EP.U3D.LIBRARY.NET;

namespace EP.U3D.LIBRARY.PATCH
{
    public class Patcher
    {
#if EFRAME_ILR || EFRAME_LUA
        public FileManifest LocalAssetManifest;
        public FileManifest RemoteAssetManifest;
        public FileManifest StreamingAssetManifest;
        public FileManifest.DifferInfo AssetDifferInfo;
#endif

#if EFRAME_ILR
        public FileManifest LocalILRManifest;
        public FileManifest RemoteILRManifest;
        public FileManifest StreamingILRManifest;
        public FileManifest.DifferInfo ILRDifferInfo;
#endif

#if EFRAME_LUA
        public FileManifest LocalLUAManifest;
        public FileManifest RemoteLUAManifest;
        public FileManifest StreamingLUAManifest;
        public FileManifest.DifferInfo LUADifferInfo;
#endif

        public int TotalSize;
        public int DownloadSize;

        public bool IsDone { get; set; }

        public string Error { get; set; }

        public IEnumerator Process(bool update)
        {
            IsDone = false;
#if EFRAME_ILR || EFRAME_LUA
            // initialize asset manifest.
            StreamingAssetManifest = new FileManifest(Constants.STREAMING_ASSET_BUNDLE_PATH, Constants.MANIFEST_FILE);
            yield return Loom.StartCR(StreamingAssetManifest.Initialize(true, false));
            LocalAssetManifest = new FileManifest(Constants.LOCAL_ASSET_BUNDLE_PATH, Constants.MANIFEST_FILE);
            yield return Loom.StartCR(LocalAssetManifest.Initialize(false, false));
            if (update)
            {
                RemoteAssetManifest = new FileManifest(Constants.REMOTE_ASSET_BUNDLE_PATH, Constants.MANIFEST_FILE);
                yield return Loom.StartCR(RemoteAssetManifest.Initialize(false, true));
                Error = RemoteAssetManifest.Error;
                if (string.IsNullOrEmpty(Error) == false)
                {
                    IsDone = true;
                    yield break;
                }
            }
#endif

#if EFRAME_ILR
            // initialize ilr script manifest.
            StreamingILRManifest = new FileManifest(Constants.STREAMING_ILR_BUNDLE_PATH, Constants.MANIFEST_FILE);
            yield return Loom.StartCR(StreamingILRManifest.Initialize(true, false));
            LocalILRManifest = new FileManifest(Constants.LOCAL_ILR_BUNDLE_PATH, Constants.MANIFEST_FILE);
            yield return Loom.StartCR(LocalILRManifest.Initialize(false, false));
            if (update)
            {
                RemoteILRManifest = new FileManifest(Constants.REMOTE_ILR_BUNDLE_PATH, Constants.MANIFEST_FILE);
                yield return Loom.StartCR(RemoteILRManifest.Initialize(false, true));
                Error = RemoteILRManifest.Error;
                if (string.IsNullOrEmpty(Error) == false)
                {
                    IsDone = true;
                    yield break;
                }
            }
#endif

#if EFRAME_LUA
            // initialize lua script manifest.
            StreamingLUAManifest = new FileManifest(Constants.STREAMING_LUA_BUNDLE_PATH, Constants.MANIFEST_FILE);
            yield return Loom.StartCR(StreamingLUAManifest.Initialize(true, false));
            LocalLUAManifest = new FileManifest(Constants.LOCAL_LUA_BUNDLE_PATH, Constants.MANIFEST_FILE);
            yield return Loom.StartCR(LocalLUAManifest.Initialize(false, false));
            if (update)
            {
                RemoteLUAManifest = new FileManifest(Constants.REMOTE_LUA_BUNDLE_PATH, Constants.MANIFEST_FILE);
                yield return Loom.StartCR(RemoteLUAManifest.Initialize(false, true));
                Error = RemoteLUAManifest.Error;
                if (string.IsNullOrEmpty(Error) == false)
                {
                    IsDone = true;
                    yield break;
                }
            }
#endif

#if EFRAME_ILR || EFRAME_LUA
            // ensure local asset is ok.
            if (LocalAssetManifest.FileInfos.Count == 0)
            {
                yield return Loom.StartCR(Extract(LocalAssetManifest, Constants.LOCAL_ASSET_BUNDLE_PATH, Constants.STREAMING_ASSET_BUNDLE_PATH));
                // reload asset manifest.
                LocalAssetManifest = new FileManifest(Constants.LOCAL_ASSET_BUNDLE_PATH, Constants.MANIFEST_FILE);
                yield return LocalAssetManifest.Initialize(false, false);
                // write version.
            }
#endif

#if EFRAME_ILR
            // ensure local ilr script is ok.
            if (LocalILRManifest.FileInfos.Count == 0)
            {
                yield return Loom.StartCR(Extract(LocalILRManifest, Constants.LOCAL_ILR_BUNDLE_PATH, Constants.STREAMING_ILR_BUNDLE_PATH));
                // reload ilr script manifest.
                LocalILRManifest = new FileManifest(Constants.LOCAL_ILR_BUNDLE_PATH, Constants.MANIFEST_FILE);
                yield return LocalILRManifest.Initialize(false, false);
            }
#endif

#if EFRAME_LUA
            // ensure local lua script is ok.
            if (LocalLUAManifest.FileInfos.Count == 0)
            {
                yield return Loom.StartCR(Extract(LocalLUAManifest, Constants.LOCAL_LUA_BUNDLE_PATH, Constants.STREAMING_LUA_BUNDLE_PATH));
                // reload lua script manifest.
                LocalLUAManifest = new FileManifest(Constants.LOCAL_LUA_BUNDLE_PATH, Constants.MANIFEST_FILE);
                yield return LocalLUAManifest.Initialize(false, false);
            }
#endif

            // process update.
            TotalSize = 0;
            DownloadSize = 0;
            if (update)
            {
#if EFRAME_ILR || EFRAME_LUA
                AssetDifferInfo = LocalAssetManifest.CompareWith(RemoteAssetManifest);
                TotalSize += AssetDifferInfo.UpdateSize;
#endif

#if EFRAME_ILR
                ILRDifferInfo = LocalILRManifest.CompareWith(RemoteILRManifest);
                TotalSize += ILRDifferInfo.UpdateSize;
#endif

#if EFRAME_LUA
                LUADifferInfo = LocalLUAManifest.CompareWith(RemoteLUAManifest);
                TotalSize += LUADifferInfo.UpdateSize;
#endif

#if EFRAME_ILR || EFRAME_LUA
                yield return Update("asset", AssetDifferInfo, RemoteAssetManifest, Constants.LOCAL_ASSET_BUNDLE_PATH, Constants.REMOTE_ASSET_BUNDLE_PATH);
                if (string.IsNullOrEmpty(Error) == false)
                {
                    IsDone = true;
                    yield break;
                }
#endif

#if EFRAME_ILR
                yield return Update("ilr", ILRDifferInfo, RemoteILRManifest, Constants.LOCAL_ILR_BUNDLE_PATH, Constants.REMOTE_ILR_BUNDLE_PATH);
                if (string.IsNullOrEmpty(Error) == false)
                {
                    IsDone = true;
                    yield break;
                }
#endif

#if EFRAME_LUA
                yield return Update("lua", LUADifferInfo, RemoteLUAManifest, Constants.LOCAL_LUA_BUNDLE_PATH, Constants.REMOTE_LUA_BUNDLE_PATH);
                if (string.IsNullOrEmpty(Error) == false)
                {
                    IsDone = true;
                    yield break;
                }
#endif
            }
            IsDone = true;
            yield return 0;
        }

        private IEnumerator Update(string tag, FileManifest.DifferInfo differInfo, FileManifest manifest, string localPath, string remotePath)
        {
            Error = string.Empty;
            if (differInfo.HasDiffer == false)
            {
                Helper.Log(Constants.RELEASE_MODE ? null : "local {0} is same with remote,no need to update.", tag);
                yield break;
            }
            else
            {
                if (differInfo.NeedUpdate)
                {
                    Updater.Instance.SetUpdateTips(Updater.Instance.TIPS1005);
                    Updater.Instance.SetUpdateProgress(DownloadSize, TotalSize);
                }
                if (differInfo.Modified.Count > 0)
                {
                    // 下载服务器差异的文件
                    yield return Loom.StartCR(Download(differInfo.Modified, manifest, localPath, remotePath));
                }
                if (string.IsNullOrEmpty(Error) == false)
                {
                    yield break;
                }
                if (differInfo.Added.Count > 0)
                {
                    // 下载服务器新增的文件
                    yield return Loom.StartCR(Download(differInfo.Added, manifest, localPath, remotePath));
                }
                if (string.IsNullOrEmpty(Error) == false)
                {
                    yield break;
                }
                if (differInfo.Deleted.Count > 0)
                {
                    // 删除服务器不存在的文件
                    yield return Loom.StartCR(Clean(differInfo.Deleted, localPath));
                }
                Error = string.Empty;
                yield return 0;
            }
        }

        private IEnumerator Extract(FileManifest manifest, string localPath, string streamingPath)
        {
            Helper.Log(Constants.RELEASE_MODE ? null : "start.");
            Helper.Log(Constants.RELEASE_MODE ? null : "local path is {0}.", localPath);
            Helper.Log(Constants.RELEASE_MODE ? null : "streaming path is {0}.", streamingPath);
            if (Directory.Exists(localPath)) Directory.Delete(localPath, true);
            Directory.CreateDirectory(localPath);
            // 拷贝清单里的其他文件
            for (int i = 0; i < manifest.FileInfos.Count; i++)
            {
                FileManifest.FileInfo fileInfo = manifest.FileInfos[i];
                string contentFilePath = streamingPath + fileInfo.Name;
                string localFilePath = localPath + fileInfo.Name;
                Helper.Log(Constants.RELEASE_MODE ? null : "extract {0} to {1}.", contentFilePath, localFilePath);
                if (Application.platform == RuntimePlatform.Android)
                {
                    WWW www = new WWW(contentFilePath);
                    yield return www;
                    if (www.isDone)
                    {
                        try
                        {
                            Helper.SaveFile(localFilePath, www.bytes);
                        }
                        catch (Exception e)
                        {
                            Error = "File Write Error!";
                            Helper.Log(Constants.RELEASE_MODE ? null : "ExtractStreamingAsset error: {0}.", e.Message);
                        }
                    }
                    yield return 0;
                }
                else
                {
                    if (Helper.HasFile(localFilePath))
                    {
                        Helper.DeleteFile(localFilePath);
                    }
                    // NOTICE: 文件路径超过255，会找不到
                    Helper.CopyFile(contentFilePath, localFilePath, true);
                }
                yield return new WaitForEndOfFrame();
            }
            // 保存manifest文件至本地
            if (manifest.Bytes != null)
            {
                string localManifestPath = localPath + Constants.MANIFEST_FILE;
                try
                {
                    Helper.SaveFile(localManifestPath, manifest.Bytes);
                }
                catch (Exception e)
                {
                    Error = "File Write Error!";
                    Helper.Log(Constants.RELEASE_MODE ? null : "ExtractStreamingAsset error: {0}.", e.Message);
                }
            }
            Helper.Log(Constants.RELEASE_MODE ? null : "done.");
            yield return 0;
        }

        private IEnumerator Download(List<FileManifest.FileInfo> fileInfos, FileManifest manifest, string localPath, string remotePath)
        {
            Helper.Log(Constants.RELEASE_MODE ? null : "start.");
            Helper.Log(Constants.RELEASE_MODE ? null : "local path is {0}.", localPath);

            WWW www = null;
            for (int i = 0; i < fileInfos.Count; i++)
            {
                if (Updater.Instance.PatchHttp.CurrentStatus == HttpListener.Status.HostError || Updater.Instance.PatchHttp.CurrentStatus == HttpListener.Status.NetworkError)
                {
                    Error = "ServerError";
                    yield break;
                }
                FileManifest.FileInfo fileInfo = fileInfos[i];
                string remoteFilePath = remotePath + fileInfo.Name;
                string localFilePath = localPath + fileInfo.Name;
                Helper.Log(Constants.RELEASE_MODE ? null : "download from {0} to {1}.", remoteFilePath, localFilePath);
                www = new WWW(remoteFilePath);
                yield return www;
                if (string.IsNullOrEmpty(www.error) && www.isDone)
                {
                    DownloadSize += fileInfo.Size;
                    Updater.Instance.SetUpdateProgress(DownloadSize, TotalSize);
                    try
                    {
                        Helper.SaveFile(localFilePath, www.bytes);
                    }
                    catch (Exception e)
                    {
                        Error = "File Write Error!";
                        Helper.Log(Constants.RELEASE_MODE ? null : "DownloadAsset error: {0}.", e.Message);
                    }
                    yield return new WaitForEndOfFrame();
                }
                else
                {
                    Error = www.error;
                    www.Dispose();
                    yield break;
                }
            }
            www.Dispose();

            // 保存manifest文件至本地
            string localManifestPath = localPath + Constants.MANIFEST_FILE;
            try
            {
                Helper.SaveFile(localManifestPath, manifest.Bytes);
            }
            catch (Exception e)
            {
                Error = "File Write Error!";
                Helper.Log(Constants.RELEASE_MODE ? null : "DownloadAsset error: {0}.", e.Message);
            }

            Helper.Log(Constants.RELEASE_MODE ? null : "done.");
            yield return 0;
        }

        private IEnumerator Clean(List<FileManifest.FileInfo> fileInfos, string localPath)
        {
            Helper.Log(Constants.RELEASE_MODE ? null : "start.");
            Helper.Log(Constants.RELEASE_MODE ? null : "local path is {0}.", localPath);

            for (int i = 0; i < fileInfos.Count; i++)
            {
                FileManifest.FileInfo fileInfo = fileInfos[i];
                string filePath = localPath + fileInfo.Name;

                if (Helper.HasFile(filePath))
                {
                    Helper.Log(Constants.RELEASE_MODE ? null : "delete {0}.", filePath);
                    Helper.DeleteFile(filePath);
                }

            }
            Helper.Log(Constants.RELEASE_MODE ? null : "done.");
            yield return 0;
        }
    }
}