﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using agora_gaming_rtc;
using agora_utilities;
using AgoraNative;
using Random = UnityEngine.Random;

public class DesktopScreenShare : MonoBehaviour
{
    [SerializeField] private string APP_ID = "";

    [SerializeField] private string TOKEN = "";

    [SerializeField] private string CHANNEL_NAME = "YOUR_CHANNEL_NAME";

    private IRtcEngine mRtcEngine;
    private uint remoteUid = 0;
    private const float Offset = 100;
    public Text logText;
    private Logger _logger;
    private Dropdown _winIdSelect;
    private Button _startShareBtn;
    private Button _stopShareBtn;

    // Use this for initialization
    void Start()
    {
        _logger = new Logger(logText);
        CheckAppId();
        InitEngine();
        JoinChannel();
        PrepareScreenCapture();
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void CheckAppId()
    {
        _logger.DebugAssert(APP_ID.Length > 10, "Please fill in your appId in VideoCanvas!!!!!");
    }

    private void JoinChannel()
    {
        mRtcEngine.JoinChannelByKey(TOKEN, CHANNEL_NAME);
    }

    private void InitEngine()
    {
        mRtcEngine = IRtcEngine.GetEngine(APP_ID);
        mRtcEngine.SetLogFile("log.txt");
        mRtcEngine.EnableAudio();
        mRtcEngine.EnableVideo();
        mRtcEngine.EnableVideoObserver();
        mRtcEngine.OnJoinChannelSuccess += OnJoinChannelSuccessHandler;
        mRtcEngine.OnLeaveChannel += OnLeaveChannelHandler;
        mRtcEngine.OnWarning += OnSDKWarningHandler;
        mRtcEngine.OnError += OnSDKErrorHandler;
        mRtcEngine.OnConnectionLost += OnConnectionLostHandler;
        mRtcEngine.OnUserJoined += OnUserJoinedHandler;
        mRtcEngine.OnUserOffline += OnUserOfflineHandler;
    }


    private void PrepareScreenCapture()
    {
        _winIdSelect = GameObject.Find("winIdSelect").GetComponent<Dropdown>();

        if (_winIdSelect != null)
        {
            _winIdSelect.ClearOptions();
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            var macDispIdList = AgoraNativeBridge.GetMacDisplayIds();
            if (macDispIdList != null)
            {
                _winIdSelect.AddOptions(macDispIdList.Select(w =>
                    new Dropdown.OptionData(
                        string.Format("Display {0}", w))).ToList());
            }

            var macWinIdList = AgoraNativeBridge.GetMacWindowList();
            if (macWinIdList != null)
            {
                _winIdSelect.AddOptions(macWinIdList.windows.Select(w =>
                        new Dropdown.OptionData(
                            string.Format("{0, -20} | {1}", w.kCGWindowOwnerName, w.kCGWindowNumber)))
                    .ToList());
            }
#else
#endif
        }

        _startShareBtn = GameObject.Find("startShareBtn").GetComponent<Button>();
        _stopShareBtn = GameObject.Find("stopShareBtn").GetComponent<Button>();
        if (_startShareBtn != null) _startShareBtn.onClick.AddListener(OnStartShareBtnClick);
        if (_stopShareBtn != null)
        {
            _stopShareBtn.onClick.AddListener(OnStopShareBtnClick);
            _stopShareBtn.gameObject.SetActive(false);
        }
    }

    private void OnStartShareBtnClick()
    {
        if (_startShareBtn != null) _startShareBtn.gameObject.SetActive(false);
        if (_stopShareBtn != null) _stopShareBtn.gameObject.SetActive(true);
        mRtcEngine.StopScreenCapture();

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        if (_winIdSelect == null) return;
        var option = _winIdSelect.options[_winIdSelect.value].text;
        if (string.IsNullOrEmpty(option)) return;
        if (option.Contains("|"))
        {
            var windowId = option.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)[1];
            _logger.UpdateLog(string.Format(">>>>> Start sharing {0}", windowId));
            mRtcEngine.StartScreenCaptureByWindowId(int.Parse(windowId), default(Rectangle),
                default(ScreenCaptureParameters));
        }
        else
        {
            var dispId = uint.Parse(option.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)[1]);
            _logger.UpdateLog(string.Format(">>>>> Start sharing display {0}", dispId));
            mRtcEngine.StartScreenCaptureByDisplayId(dispId, default(Rectangle),
                new ScreenCaptureParameters {captureMouseCursor = true, frameRate = 30});
        }

#else
#endif
    }

    private void OnStopShareBtnClick()
    {
        if (_startShareBtn != null) _startShareBtn.gameObject.SetActive(true);
        if (_stopShareBtn != null) _stopShareBtn.gameObject.SetActive(false);
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        mRtcEngine.StopScreenCapture();
#else
#endif
    }

    private void OnJoinChannelSuccessHandler(string channelName, uint uid, int elapsed)
    {
        _logger.UpdateLog(string.Format("sdk version: ${0}", IRtcEngine.GetSdkVersion()));
        _logger.UpdateLog(string.Format("onJoinChannelSuccess channelName: {0}, uid: {1}, elapsed: {2}", channelName,
            uid, elapsed));
        makeVideoView(0);
    }

    private void OnLeaveChannelHandler(RtcStats stats)
    {
        _logger.UpdateLog("OnLeaveChannelSuccess");
        DestroyVideoView(0);
    }

    private void OnUserJoinedHandler(uint uid, int elapsed)
    {
        if (remoteUid == 0) remoteUid = uid;
        _logger.UpdateLog(string.Format("OnUserJoined uid: ${0} elapsed: ${1}", uid, elapsed));
        makeVideoView(uid);
    }

    private void OnUserOfflineHandler(uint uid, USER_OFFLINE_REASON reason)
    {
        remoteUid = 0;
        _logger.UpdateLog(string.Format("OnUserOffLine uid: ${0}, reason: ${1}", uid, (int) reason));
        DestroyVideoView(uid);
    }

    private void OnSDKWarningHandler(int warn, string msg)
    {
        _logger.UpdateLog(string.Format("OnSDKWarning warn: {0}, msg: {1}", warn, msg));
    }

    private void OnSDKErrorHandler(int error, string msg)
    {
        _logger.UpdateLog(string.Format("OnSDKError error: {0}, msg: {1}", error, msg));
    }

    private void OnConnectionLostHandler()
    {
        _logger.UpdateLog("OnConnectionLost ");
    }

    void OnApplicationQuit()
    {
        Debug.Log("OnApplicationQuit");
        if (mRtcEngine != null)
        {
            mRtcEngine.LeaveChannel();
            mRtcEngine.DisableVideoObserver();
            IRtcEngine.Destroy();
        }
    }

    private void DestroyVideoView(uint uid)
    {
        var go = GameObject.Find(uid.ToString());
        if (!ReferenceEquals(go, null))
        {
            Destroy(go);
        }
    }

    private void makeVideoView(uint uid)
    {
        var go = GameObject.Find(uid.ToString());
        if (!ReferenceEquals(go, null))
        {
            return; // reuse
        }

        // create a GameObject and assign to this new user
        var videoSurface = makeImageSurface(uid.ToString());
        if (!ReferenceEquals(videoSurface, null))
        {
            // configure videoSurface
            videoSurface.SetForUser(uid);
            videoSurface.SetEnable(true);
            videoSurface.SetVideoSurfaceType(AgoraVideoSurfaceType.RawImage);
            videoSurface.SetGameFps(30);
            videoSurface.EnableFilpTextureApply(true, false);
        }
    }

    // VIDEO TYPE 1: 3D Object
    public VideoSurface makePlaneSurface(string goName)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);

        if (go == null)
        {
            return null;
        }

        go.name = goName;
        // set up transform
        go.transform.Rotate(-90.0f, 0.0f, 0.0f);
        var yPos = Random.Range(3.0f, 5.0f);
        var xPos = Random.Range(-2.0f, 2.0f);
        go.transform.position = new Vector3(xPos, yPos, 0f);
        go.transform.localScale = new Vector3(0.25f, 0.5f, .5f);

        // configure videoSurface
        var videoSurface = go.AddComponent<VideoSurface>();
        return videoSurface;
    }

    // Video TYPE 2: RawImage
    public VideoSurface makeImageSurface(string goName)
    {
        var go = new GameObject();

        if (go == null)
        {
            return null;
        }

        go.name = goName;
        // to be renderered onto
        go.AddComponent<RawImage>();
        // make the object draggable
        go.AddComponent<UIElementDrag>();
        var canvas = GameObject.Find("VideoCanvas");
        if (canvas != null)
        {
            go.transform.parent = canvas.transform;
            Debug.Log("add video view");
        }
        else
        {
            Debug.Log("Canvas is null video view");
        }

        // set up transform
        go.transform.Rotate(0.0f, 0.0f, 180.0f);
        var xPos = Random.Range(Offset - Screen.width / 2f, Screen.width / 2f - Offset);
        var yPos = Random.Range(Offset, Screen.height / 2f - Offset);
        go.transform.localPosition = new Vector3(xPos, yPos, 0f);
        go.transform.localScale = new Vector3(3f, 4f, 1f);

        // configure videoSurface
        var videoSurface = go.AddComponent<VideoSurface>();
        return videoSurface;
    }
}