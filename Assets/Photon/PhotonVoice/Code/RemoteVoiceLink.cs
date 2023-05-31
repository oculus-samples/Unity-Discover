namespace Photon.Voice.Unity
{
    using Voice;
    using System;

    public class RemoteVoiceLink
    {
        public readonly VoiceInfo VoiceInfo;
        public readonly int PlayerId;
        public readonly byte VoiceId;
        public readonly int ChannelId;

        public event Action<FrameOut<float>> FloatFrameDecoded;
        public event Action RemoteVoiceRemoved;

        public RemoteVoiceLink(VoiceInfo info, int playerId, byte voiceId, int channelId, ref RemoteVoiceOptions options)
        {
            this.VoiceInfo = info;
            this.PlayerId = playerId;
            this.VoiceId = voiceId;
            this.ChannelId = channelId;
            options.SetOutput(this.OnDecodedFrameFloatAction);
            options.OnRemoteVoiceRemoveAction = this.OnRemoteVoiceRemoveAction;
        }

        private void OnRemoteVoiceRemoveAction()
        {
            if (this.RemoteVoiceRemoved != null)
            {
                this.RemoteVoiceRemoved();
            }
        }

        private void OnDecodedFrameFloatAction(FrameOut<float> floats)
        {
            if (this.FloatFrameDecoded != null)
            {
                this.FloatFrameDecoded(floats);
            }
        }

        private string cached;
        public override string ToString()
        {
            if (string.IsNullOrEmpty(this.cached))
            {
                this.cached = string.Format("[p#{0} v#{1} c#{2} i:{{{3}}}]", this.PlayerId, this.VoiceId, this.ChannelId, this.VoiceInfo);
            }
            return this.cached;
        }
    }
}
