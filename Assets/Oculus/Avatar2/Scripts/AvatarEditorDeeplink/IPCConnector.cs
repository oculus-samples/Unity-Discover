#if UNITY_EDITOR || UNITY_STANDALONE_WIN
#if !DISABLE_IPC_CONNECTOR
using System.Collections.Generic;
using Daybreak.IPC;
using Newtonsoft.Json;

internal class IpcOafConnector : OafConnector
{
    private static IpcOafConnector _instance;
    public static IpcOafConnector Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new IpcOafConnector();
            }

            return _instance;
        }
    }

    public static bool hasInstance => _instance != null;

    protected override string LogChannel => "OafIpc";

    private IpcOafConnector()
    {
        Init();
    }

    // Unity 2020 seems to have issues with multiple requests using the same connector,
    // so we need a way to recreate the connector
    public static IpcOafConnector Recreate()
    {
        if (_instance != null)
        {
            _instance.Destroy();
        }

        return Instance;
    }

    public new void Destroy()
    {
        _instance = null;
        base.Destroy();
    }

    public static void RequestWork(
        string endPoint,
        Dictionary<string, object> dict,
        SendPayload.SendCallbackCoroutine callbackCoroutine)
    {
        Instance.SendPayloadToOaf(
            new SendPayload
            {
                requestName = endPoint,
                requestData = new DummyPayload(dict),
                OnRecvResponse = callbackCoroutine
            });
    }
    
    private class DummyPayload : SendInnerDataBase
    {
        public DummyPayload(Dictionary<string, object> dict)
        {
            json = JsonConvert.SerializeObject(dict);
        }

        public override string PayloadToString()
        {
            return json;
        }

        private string json;
    }
}
#endif // !DISABLE_IPC_CONNECTOR
#endif //UNITY_EDITOR || UNITY_STANDALONE_WIN
