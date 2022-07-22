//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using UnityEngine.UI;
using EP.U3D.LIBRARY.BASE;
using EP.U3D.LIBRARY.NET;
using EP.U3D.LIBRARY.ASSET;
using EP.U3D.LIBRARY.REPORT;
using EP.U3D.LIBRARY.UI;
using EP.U3D.LIBRARY.JSON;

namespace EP.U3D.LIBRARY.PATCH
{
    public class Updater : MonoBehaviour
    {
        #region Update Logic
        public static Updater Instance;

        public string TIPS1001 = "网络异常，错误码1001";
        public string TIPS1002 = "网络异常，错误码1002";
        public string TIPS1003 = "资源异常，请重启应用";
        public string TIPS1004 = "网络异常，错误码1004";
        public string TIPS1005 = "正在下载必要的资源，请不要断开网络";
        public string TIPS1006 = "当前网络不可用，请检查设备是否连接至互联网";
        public string TIPS1007 = "正在初始化";
        public string TIPS1008 = "发现新版本，前往下载";
        public string TIPS1009 = "正在加载应用";
        public string TIPS1010 = "正在进入应用";

        public static event Action OnStarted;
        public static event Action OnFinished;
        public Patcher Patcher;
        public Coroutine JsonTO;
        public HttpListener JsonHttp;
        public HttpListener PatchHttp;

        public static void Initialize(Action onStarted, Action onFinished)
        {
            AssetManager.Initialize();
            GameObject go = null;
            if (AssetManager.OK) go = AssetManager.LoadAsset(Constants.UPDATER_PREFAB_PATH, typeof(GameObject)) as GameObject; // 优先加载bundle
            if (go == null) go = AssetManager.LoadAsset(Constants.UPDATER_PREFAB_PATH, typeof(GameObject), true) as GameObject; // 否则加载resources
            UIHelper.CloneGO(go);
            OnStarted += onStarted;
            OnFinished += onFinished;
        }

        public virtual IEnumerator JsonTimeout()
        {
            yield return new WaitForSeconds(8);
            if (JsonHttp != null)
            {
                JsonHttp.Stop();
            }
            JsonTO = null;
        }

        public virtual void ReqJson()
        {
            Helper.Log(Constants.RELEASE_MODE ? null : "[JSON][WWW@{0}]", Constants.JSON_FILE);
            string localJson = Helper.StringFormat("{0}Game.json", Constants.CONFIG_PATH);
            JsonHttp = new HttpListener(Constants.JSON_FILE, 2, (HttpListener.Status last, HttpListener.Status current, WWW www) =>
            {
                Helper.Log(Constants.RELEASE_MODE ? null : "last is {0},current is {1}.", last.ToString(), current.ToString());
                if (current == HttpListener.Status.NetworkError)
                {
                    SetUpdateTips(TIPS1006);
                }
                else if (current == HttpListener.Status.HostError)
                {
                    SetUpdateTips(TIPS1001);
                    byte[] a = Helper.OpenFile(localJson);
                    if (a != null && a.Length > 0)
                    {
                        string b = Encoding.UTF8.GetString(a);
                        if (ParseJson(b) == false)
                        {
                            SetUpdateTips(TIPS1002);
                        }
                        else
                        {
                            AfterJson();
                            JsonTO = Loom.StartCR(JsonTimeout());
                        }
                    }
                }
                else if (current == HttpListener.Status.OK)
                {
                    JsonHttp.Stop();
                    if (JsonTO == null)
                    {
                        if (ParseJson(www.text) == false)
                        {
                            SetUpdateTips(TIPS1002);
                        }
                        else
                        {
                            Helper.SaveFile(localJson, Encoding.UTF8.GetBytes(www.text));
                            AfterJson();
                        }
                    }
                    else
                    {
                        Helper.SaveFile(localJson, Encoding.UTF8.GetBytes(www.text));
                        Loom.StopCR(JsonTO);
                        JsonTO = null;
                    }
                }
            });
            SetUpdateTips(TIPS1007);
            JsonHttp.Start();
        }

        public virtual bool ParseJson(string json)
        {
            try
            {
                Constants.JSON_DATA = json;
                JsonReader reader = new JsonReader(json);
                Dictionary<string, object> dic = JsonMapper.ToObject<Dictionary<string, object>>(reader);
                if (dic == null || dic.Count == 0)
                {
                    Helper.LogError(Constants.RELEASE_MODE ? null : "Decode json error: nil content.");
                    return false;
                }
                if (Application.isEditor)
                {
                    if (dic.ContainsKey("check_mode"))
                    {
                        dic.Remove("check_mode");
                    }
                    dic.Add("check_mode", Preferences.Instance.CheckMode ? "1" : "0");
                }
                if (Constants.LIVE_MODE)
                {
                    Constants.CHECK_UPDATE = true;
                    Constants.FORCE_UPDATE = false;
                }
                else
                {
                    Constants.CHECK_UPDATE = Preferences.Instance.CheckUpdate;
                    Constants.FORCE_UPDATE = Preferences.Instance.ForceUpdate;
                }
                IDictionaryEnumerator ir = dic.GetEnumerator();
                for (int i = 0; i < dic.Count; i++)
                {
                    ir.MoveNext();
                    KeyValuePair<string, string> kvp = new KeyValuePair<string, string>(ir.Key as string, ir.Value as string);
                    switch (kvp.Key)
                    {
                        case "newest_version":
                            Constants.NEWEST_VERSION = kvp.Value;
                            break;
                        case "binary_url":
                            Constants.BINARY_FILE_URL = kvp.Value;
                            break;
                        case "binary_size":
                            int size = 0;
                            int.TryParse(kvp.Value, out size);
                            Constants.BINARY_FILE_SIZE = size;
                            break;
                        case "patch_url":
                            if (Constants.LIVE_MODE == false)
                            {
                                if (string.IsNullOrEmpty(Preferences.Instance.PatchServer))
                                {
                                    Constants.REMOTE_FILE_BUNDLE_ROOT = kvp.Value + "/";
                                    Constants.REMOTE_ASSET_BUNDLE_PATH = kvp.Value + "/" + Constants.PLATFORM_NAME + "/Assets/";
                                    Constants.REMOTE_ILR_BUNDLE_PATH = kvp.Value + "/" + Constants.PLATFORM_NAME + "/Scripts/ILR/";
                                    Constants.REMOTE_LUA_BUNDLE_PATH = IntPtr.Size == 4 ?
                                        kvp.Value + "/" + Constants.PLATFORM_NAME + "/Scripts/LUA/x86/" :
                                        kvp.Value + "/" + Constants.PLATFORM_NAME + "/Scripts/LUA/x64/";
                                }
                                else
                                {
                                    Constants.REMOTE_FILE_BUNDLE_ROOT = Preferences.Instance.PatchServer + "/";
                                    Constants.REMOTE_ASSET_BUNDLE_PATH = Preferences.Instance.PatchServer + "/" + Constants.PLATFORM_NAME + "/Assets/";
                                    Constants.REMOTE_ILR_BUNDLE_PATH = Preferences.Instance.PatchServer + "/" + Constants.PLATFORM_NAME + "/Scripts/ILR/";
                                    Constants.REMOTE_LUA_BUNDLE_PATH = IntPtr.Size == 4 ?
                                        Preferences.Instance.PatchServer + "/" + Constants.PLATFORM_NAME + "/Scripts/LUA/x86/" :
                                        Preferences.Instance.PatchServer + "/" + Constants.PLATFORM_NAME + "/Scripts/LUA/x64/";
                                }
                            }
                            else
                            {
                                Constants.REMOTE_FILE_BUNDLE_ROOT = kvp.Value + "/";
                                Constants.REMOTE_ASSET_BUNDLE_PATH = kvp.Value + "/" + Constants.PLATFORM_NAME + "/Assets/";
                                Constants.REMOTE_ILR_BUNDLE_PATH = kvp.Value + "/" + Constants.PLATFORM_NAME + "/Scripts/ILR/";
                                Constants.REMOTE_LUA_BUNDLE_PATH = IntPtr.Size == 4 ?
                                    kvp.Value + "/" + Constants.PLATFORM_NAME + "/Scripts/LUA/x86/" :
                                    kvp.Value + "/" + Constants.PLATFORM_NAME + "/Scripts/LUA/x64/";
                            }
                            break;
                        case "force_update":
                            if (Constants.LIVE_MODE)
                            {
                                int forceupdate = 0;
                                int.TryParse(kvp.Value, out forceupdate);
                                Constants.FORCE_UPDATE = forceupdate == 1;
                            }
                            break;
                        case "conn_ip":
                            if (Constants.LIVE_MODE == false)
                            {
                                var connServer = "";
                                if (Preferences.Instance.ConnIndex > 0
                                    && Preferences.Instance.ConnServer.Count > Preferences.Instance.ConnIndex
                                    && Preferences.Instance.ConnServer[Preferences.Instance.ConnIndex] != "NONE")
                                {
                                    connServer = Preferences.Instance.ConnServer[Preferences.Instance.ConnIndex];
                                }
                                if (string.IsNullOrEmpty(connServer) == false)
                                {
                                    try
                                    {
                                        Constants.CONN_SERVER_IP = Preferences.Instance.ConnServer[Preferences.Instance.ConnIndex].Split(':')[0];
                                    }
                                    catch { Constants.CONN_SERVER_IP = kvp.Value; }
                                }
                                else
                                {
                                    Constants.CONN_SERVER_IP = kvp.Value;
                                }
                            }
                            else
                            {
                                Constants.CONN_SERVER_IP = kvp.Value;
                            }
                            break;
                        case "conn_port":
                            int port = 0;
                            int.TryParse(kvp.Value, out port);
                            if (Constants.LIVE_MODE == false)
                            {
                                var connServer = "";
                                if (Preferences.Instance.ConnIndex > 0
                                    && Preferences.Instance.ConnServer.Count > Preferences.Instance.ConnIndex
                                    && Preferences.Instance.ConnServer[Preferences.Instance.ConnIndex] != "NONE")
                                {
                                    connServer = Preferences.Instance.ConnServer[Preferences.Instance.ConnIndex];
                                }
                                if (string.IsNullOrEmpty(connServer) == false)
                                {
                                    try
                                    {
                                        int.TryParse(connServer.Split(':')[1], out port);
                                        Constants.CONN_SERVER_PORT = port;
                                    }
                                    catch
                                    {
                                        int.TryParse(kvp.Value, out port);
                                        Constants.CONN_SERVER_PORT = port;
                                    }
                                }
                                else
                                {
                                    Constants.CONN_SERVER_PORT = port;
                                }
                            }
                            else
                            {
                                Constants.CONN_SERVER_PORT = port;
                            }
                            break;
                        case "cgi_url":
                            if (Constants.LIVE_MODE == false)
                            {
                                var cgiServer = "";
                                if (Preferences.Instance.CgiIndex > 0
                                    && Preferences.Instance.CgiServer.Count > Preferences.Instance.CgiIndex
                                    && Preferences.Instance.CgiServer[Preferences.Instance.CgiIndex] != "NONE")
                                {
                                    cgiServer = Preferences.Instance.CgiServer[Preferences.Instance.CgiIndex];
                                }
                                if (string.IsNullOrEmpty(cgiServer) == false)
                                {
                                    Constants.CGI_SERVER_URL = cgiServer;
                                }
                                else
                                {
                                    Constants.CGI_SERVER_URL = kvp.Value;
                                }
                            }
                            else
                            {
                                Constants.CGI_SERVER_URL = kvp.Value;
                            }
                            break;
                        case "report_url":
                            if (!Constants.LIVE_MODE && string.IsNullOrEmpty(Preferences.Instance.LogServer))
                            {
                                Constants.REPORT_URL = Preferences.Instance.LogServer;
                            }
                            else
                            {
                                Constants.REPORT_URL = kvp.Value;
                            }
                            break;
                        case "check_mode":
                            int check_mode = 0;
                            int.TryParse(kvp.Value, out check_mode);
                            Constants.CHECK_MODE = check_mode == 1;
                            break;
                        case "update_whitelist":
                            if (Constants.LIVE_MODE)
                            {
                                if (string.IsNullOrEmpty(kvp.Value) == false)
                                {
                                    string[] strs = kvp.Value.Split(',');
                                    if (strs != null && strs.Length > 0)
                                    {
                                        List<string> list = new List<string>(strs);
                                        Constants.UPDATE_WHITELIST = list;
                                    }
                                }
                            }
                            break;
                        case "log_whitelist":
                            if (Constants.LIVE_MODE)
                            {
                                if (string.IsNullOrEmpty(kvp.Value) == false)
                                {
                                    string[] strs = kvp.Value.Split(',');
                                    if (strs != null && strs.Length > 0)
                                    {
                                        List<string> list = new List<string>(strs);
                                        Constants.LOG_WHITELIST = list;
                                        Constants.RELEASE_MODE = !list.Contains(Constants.DEVICE_ID);
                                    }
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Helper.LogError(Constants.RELEASE_MODE ? null : "Decode json error: {0}", e.Message);
                return false;
            }
            return true;
        }

        public virtual void AfterJson()
        {
            if (!Constants.RELEASE_MODE)
            {
                Reporter.CommitVerbose();
                Reporter.CommitException();
            }
#if EFRAME_ILR || EFRAME_LUA 
            if (Constants.CHECK_MODE)
            {
                OnFinished?.Invoke();
            }
            else
            {

                if (Constants.CHECK_UPDATE)
                {
                    CheckUpdate();
                }
                else
                {
                    Loom.StartCR(Finish());
                }
            }
#else
            OnFinished?.Invoke();
#endif
        }

        public virtual void CheckUpdate()
        {
            int lastDotIndex = Constants.NEWEST_VERSION.LastIndexOf('.');
            long newestBinaryVersion = Helper.VersionToNumber(Constants.NEWEST_VERSION.Substring(0, lastDotIndex));
            long newestPatchVersion = Helper.VersionToNumber(Constants.NEWEST_VERSION.Substring(lastDotIndex + 1));
            long localBinaryVersion = Helper.VersionToNumber(Constants.BINARY_VERSION);
            long localPatchVersion = Helper.VersionToNumber(Constants.PATCH_VERSION);

            if (newestBinaryVersion > localBinaryVersion && Constants.CHECK_MODE == false)
            {
                OpenConfirm(TIPS1008, false, () => { Application.OpenURL(Constants.BINARY_FILE_URL); });
                SetUpdateTips(TIPS1008);
            }
            else
            {
                Loom.StartCR(UpdatePatch(localPatchVersion, newestPatchVersion));
            }
        }

        public virtual IEnumerator UpdatePatch(long localPatchVersion, long newestPatchVersion)
        {
            bool update = false;
            if (Constants.CHECK_MODE)
            {
                update = false;
            }
            else if (Constants.UPDATE_WHITELIST != null && !Constants.UPDATE_WHITELIST.Contains(Constants.DEVICE_ID))
            {
                update = false;
            }
            else if (Constants.FORCE_UPDATE)
            {
                update = true;
            }
            else
            {
                if (localPatchVersion != newestPatchVersion)
                {
                    update = true;
                }
                else
                {
                    update = false;
                }
            }
            if (PatchHttp != null)
            {
                PatchHttp.Stop();
            }
            PatchHttp = new HttpListener(Constants.REMOTE_FILE_BUNDLE_ROOT, 2, (HttpListener.Status last, HttpListener.Status current, WWW www) =>
             {
                 Helper.Log(Constants.RELEASE_MODE ? null : "last is " + last + ",current is " + current);
                 if (current == HttpListener.Status.NetworkError)
                 {
                     SetUpdateTips(TIPS1006);
                 }
                 else if (current == HttpListener.Status.HostError)
                 {
                     SetUpdateTips(TIPS1004);
                 }
                 if ((last == HttpListener.Status.HostError || last == HttpListener.Status.NetworkError || last == HttpListener.Status.None) && current == HttpListener.Status.OK)
                 {
                     Loom.StartCR(Patcher.Process(update));
                 }
             });
            if (update)
            {
                SetUpdateTips(TIPS1009);
                PatchHttp.Start();
                yield return new WaitUntil(() => { return Patcher.IsDone && string.IsNullOrEmpty(Patcher.Error); });
                PatchHttp.Stop();
                Constants.PATCH_VERSION = newestPatchVersion.ToString(); // 记录版本号
                SetVersion();// 刷新版本号
                yield return Loom.StartCR(Finish());
            }
            else
            {
                yield return Loom.StartCR(Patcher.Process(update));
                yield return Loom.StartCR(Finish());
            }
        }

        public virtual void Reload()
        {
#if EFRAME_ILR || EFRAME_LUA
            if (Constants.ASSET_BUNDLE_MODE)
            {
                if (!AssetManager.OK) AssetManager.Initialize();
                if (Patcher != null && Patcher.AssetDifferInfo != null)
                {
                    FileManifest.DifferInfo differ = Patcher.AssetDifferInfo;
                    bool reload = false;
                    for (int i = 0; i < differ.Modified.Count; i++)
                    {
                        var file = differ.Modified[i];
                        if (file != null)
                        {
                            if (file.Name == "Assets")
                            {
                                reload = true;
                                break;
                            }
                        }
                    }
                    if (reload)
                    {
                        AssetManager.LoadManifest();
                        Helper.Log(Constants.RELEASE_MODE ? null : "reload manifest.");
                    }
                }
            }
#endif
        }

        public virtual IEnumerator Finish()
        {
            Reload();
            SetUpdateTips(TIPS1010);
            yield return new WaitForEndOfFrame();
            OnFinished?.Invoke();
            yield return 0;
        }
        #endregion

        #region UI Logic
        public GameObject UIBG;
        public GameObject UIUpdate;
        public GameObject UIConfirm;

        private Slider updateProgressBar;
        private bool updateProgressSig;
        private float updateProgressValue;
        private bool versionPressed;
        private float versionPressedTime = -1;
        private float versionPressedKeep = -1;
        private static Text versionLB;
        private static Button versionBTN;

        public virtual void Awake()
        {
            Instance = this;
        }

        public virtual void Start()
        {
            versionLB = UIHelper.GetComponent(Instance.UIBG, "LB_Version", typeof(Text)) as Text;
            versionBTN = UIHelper.GetComponent(Instance.UIBG, "LB_Version", typeof(Button)) as Button;
            SetVersion();
            versionBTN.onPress.AddListener(() =>
            {
                versionPressed = true;
                versionPressedTime = Time.realtimeSinceStartup;
                versionLB.color = Color.white;
            });
            versionBTN.onRelease.AddListener(() =>
            {
                versionPressed = false;
                versionPressedTime = Time.realtimeSinceStartup;
                versionLB.color = Color.white;
                UIHelper.SetLocalScale(versionLB, Vector3.one);
                if (versionPressedKeep > 2.5)
                {
                    Reporter.CommitException();
                    Reporter.CommitVerbose();
                    OpenConfirm("日志提交成功", false);
                }
                else if (versionPressedKeep > 1.5)
                {
                    Helper.DeleteDirectory(Constants.LOCAL_ASSET_BUNDLE_PATH);
                    Helper.DeleteDirectory(Constants.LOCAL_ILR_BUNDLE_PATH);
                    Helper.DeleteDirectory(Constants.LOCAL_LUA_BUNDLE_PATH);
                    Helper.DeleteDirectory(Constants.CONFIG_PATH);
                    Helper.DeleteDirectory(Constants.LOG_PATH);
                    Helper.DeleteDirectory(Constants.TEMP_PATH);
                    SetUpdateTips(TIPS1003);
                    OpenConfirm("清空缓存成功", false);
                }
            });
            Patcher = new Patcher();
            ReqJson();
            OnStarted?.Invoke();
        }

        public virtual void Update()
        {
            if (versionPressed)
            {
                versionPressedKeep = Time.realtimeSinceStartup - versionPressedTime;
                if (versionPressedKeep > 2.5)
                {
                    versionLB.color = Color.red;
                    UIHelper.SetLocalScale(versionLB, new Vector3(1.3f, 1.3f, 1.3f));
                }
                else if (versionPressedKeep > 1.5)
                {
                    versionLB.color = Color.yellow;
                    UIHelper.SetLocalScale(versionLB, new Vector3(1.2f, 1.2f, 1.2f));
                }
                else if (versionPressedKeep > 0.5)
                {
                    versionLB.color = Color.green;
                    UIHelper.SetLocalScale(versionLB, new Vector3(1.1f, 1.1f, 1.1f));
                }
            }
            if (updateProgressSig)
            {
                var speed = (updateProgressValue - updateProgressBar.value) * 50;
                if (speed < 0) speed = 1;
                updateProgressBar.value = Mathf.Lerp(updateProgressBar.value, updateProgressValue, Time.deltaTime * speed);
            }
        }

        public virtual void OnDestroy()
        {
            Instance = null;
            AssetManager.UnloadAsset(Constants.UPDATER_PREFAB_PATH);
        }

        public virtual void OpenBG()
        {
            Instance.UIBG.SetActive(true);
        }

        public virtual void CloseBG()
        {
            Instance.UIBG.SetActive(false);
        }

        public virtual void SetVersion()
        {
            UIHelper.SetLabelText(versionLB, Helper.StringFormat("{0} {1}", Constants.LIVE_MODE ? "LIVE" : "TEST", Constants.LOCAL_VERSION));
        }

        public virtual void OpenUpdate()
        {
            UIHelper.SetActiveState(Instance.UIUpdate, true);
        }

        public virtual void CloseUpdate()
        {
            UIHelper.SetActiveState(Instance.UIUpdate, false);
        }

        public virtual void SetUpdateTips(string tips)
        {
            UIHelper.SetLabelText(Instance.UIUpdate, "LB_Tips", tips);
        }

        public virtual void SetTips(string tips)
        {
            UIHelper.SetLabelText(Instance.UIBG, "LB_Tips", tips);
        }

        public virtual void SetProgress(float progress, string text = "", bool status = true)
        {
            updateProgressSig = status;
            UIHelper.SetActiveState(Instance.UIUpdate, "GRP_Progress", status);
            if (status)
            {
                UIHelper.SetLabelText(Instance.UIUpdate, "GRP_Progress/LB_Percentage", string.IsNullOrEmpty(text) ? string.Format("{0}%", Mathf.FloorToInt(progress * 100)) : text);
                if (Instance.updateProgressBar == null)
                {
                    Instance.updateProgressBar = UIHelper.GetComponent(Instance.UIUpdate, "GRP_Progress", typeof(Slider)) as Slider;
                }
                Instance.updateProgressValue = progress;
            }
        }

        public virtual void SetUpdateProgress(int current, int total, bool status = true)
        {
            updateProgressSig = status;
            UIHelper.SetActiveState(Instance.UIUpdate, "GRP_Progress", status);
            if (status)
            {
                if (total < 1024 * 1024)
                {
                    UIHelper.SetLabelText(Instance.UIUpdate, "GRP_Progress/LB_Percentage", current / 1024 + "/" + total / 1024 + " KB");
                }
                else
                {
                    UIHelper.SetLabelText(Instance.UIUpdate, "GRP_Progress/LB_Percentage", (current / (1024 * 1024f)).ToString("0.00") + "/" + (total / (1024 * 1024f)).ToString("0.00") + " MB");
                }
                if (Instance.updateProgressBar == null)
                {
                    Instance.updateProgressBar = UIHelper.GetComponent(Instance.UIUpdate, "GRP_Progress", typeof(Slider)) as Slider;
                }
                Instance.updateProgressValue = (current * 1f / total);
            }
        }

        public virtual void OpenConfirm(string content, bool doubleButton, Action onClickOK = null, Action onClickCancel = null)
        {
            UIHelper.SetLabelText(Instance.UIConfirm.transform, "LB_Content", content);
            if (doubleButton)
            {
                UIHelper.SetActiveState(Instance.UIConfirm.transform, "GRP_DoubleBTN", true);
                UIHelper.SetActiveState(Instance.UIConfirm.transform, "GRP_SingleBTN", false);
                UIHelper.SetButtonEvent(Instance.UIConfirm.transform, "GRP_DoubleBTN/BTN_OK", (go) =>
                {
                    UIHelper.SetActiveState(Instance.UIConfirm, false);
                    Instance.UIConfirm.SetActive(false);
                    if (onClickOK != null)
                    {
                        onClickOK();
                    }
                });
                UIHelper.SetButtonEvent(Instance.UIConfirm.transform, "GRP_DoubleBTN/BTN_Cancel", (go) =>
                {
                    UIHelper.SetActiveState(Instance.UIConfirm, false);
                    if (onClickCancel != null)
                    {
                        onClickCancel();
                    }
                });
            }
            else
            {
                UIHelper.SetActiveState(Instance.UIConfirm.transform, "GRP_DoubleBTN", false);
                UIHelper.SetActiveState(Instance.UIConfirm.transform, "GRP_SingleBTN", true);
                UIHelper.SetButtonEvent(Instance.UIConfirm.transform, "GRP_SingleBTN/BTN_OK", (go) =>
                {
                    UIHelper.SetActiveState(Instance.UIConfirm, false);
                    if (onClickOK != null)
                    {
                        onClickOK();
                    }
                });
            }
            UIHelper.SetActiveState(Instance.UIConfirm, true);
        }

        public virtual void CloseConfirm()
        {
            UIHelper.SetActiveState(Instance.UIConfirm, false);
        }
        #endregion
    }
}