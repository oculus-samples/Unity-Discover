mergeInto(LibraryManager.library, {

    PhotonVoice_WebAudioMicIn_Start: function(handle, createCallback, dataCallback, callIntervalMs) {
        const workerFoo = function() {
            class MicCaptureProcessor extends AudioWorkletProcessor {
                process(inputs, outputs, parameters) {
                    this.port.postMessage(inputs[0][0]);
                    return true;
                }
            }
            registerProcessor('photon-voice-mic-capture-processor', MicCaptureProcessor);
        }

        if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {

            console.log('[PV] PhotonVoice_WebAudioMicIn_Start creates AudioContext and adds worklet');
            const audioContext = new AudioContext(); // we could set target sample rate here but FireFox does not support different sample rates for AudioContext and media stream
	
			// Safari: resuming mic's audio context ui handler (doesn't seem to help)
/*			
			let resumeMicAudioContext = function() {
				if (audioContext.state == 'suspended' || audioContext.state == 'interrupted') {
					console.log('[PV] PhotonVoice_WebAudioMicIn_Start resumeMicAudioContext resume', audioContext, audioContext.state);
					audioContext.resume().then(() => {
						console.log('[PV] PhotonVoice_WebAudioMicIn_Start resumeMicAudioContext resumed', audioContext, audioContext.state);
					});
				}
			}			
			window.addEventListener('mousedown', resumeMicAudioContext);
			window.addEventListener('touchstart', resumeMicAudioContext);
*/
            let ws = workerFoo.toString();
            ws = ws.substring(ws.indexOf("{") + 1, ws.lastIndexOf("}"));
            const blob = new Blob([ws], {type: "text/javascript"});

            let url = window.URL.createObjectURL(blob);
            audioContext.audioWorklet.addModule(url).then(

                function(x) {
                    // waits for the user to grant mic permission
                    navigator.mediaDevices.getUserMedia({
                            audio: true
                        })
                        .then(function(stream) {
                            // mic permission granted
                            
                            if (Module.PhotonVoice_WebAudioMicIn_InputsStopped && Module.PhotonVoice_WebAudioMicIn_InputsStopped.get(handle)) {
                                console.log('[PV] PhotonVoice_WebAudioMicIn_Start aborts due to already deleted handle ' + handle);
                                Module.PhotonVoice_WebAudioMicIn_InputsStopped.delete(handle);
                                // stopped while waiting for mic permission
                                return;
                            }
                            
                            const source = audioContext.createMediaStreamSource(stream);
                            const worklet = new AudioWorkletNode(audioContext, 'photon-voice-mic-capture-processor');

                            source.connect(worklet);
                            worklet.port.onmessage = function(e) {
                                const b = e.data;
                                const ptr = Module._malloc(b.byteLength);

                                const dataHeap = new Float32Array(HEAPU8.buffer, ptr, b.byteLength);
                                dataHeap.set(b);

                                Module.dynCall_viii(dataCallback, handle, ptr, b.length);
                                Module._free(ptr);
                            }
                            audioContext.resume();

                            console.log('[PV] PhotonVoice_WebAudioMicIn_Start input created for handle ' + handle + ': s=' + audioContext.sampleRate + ' ch=' + 1);

                            Module.PhotonVoice_WebAudioMicIn_Inputs = Module.PhotonVoice_WebAudioMicIn_Inputs || new Map();
                            Module.PhotonVoice_WebAudioMicIn_Inputs.set(handle, [audioContext, url]);
                            Module.dynCall_viiii(createCallback, handle, 0, audioContext.sampleRate, 1);
                        })
                        .catch(function(err) {
                            console.error('[PV] PhotonVoice_WebAudioMicIn_Start getUserMedia error: ' + err);
                            if (Module.PhotonVoice_WebAudioMicIn_InputsStopped) {
                                Module.PhotonVoice_WebAudioMicIn_InputsStopped.delete(handle);
                            }
                            Module.dynCall_viiii(createCallback, handle, 2, 0, 0);
                        });
                },
                function(err) {
                    console.error('[PV] PhotonVoice_WebAudioMicIn_Start error: audioContext.audioWorklet.addModule():', err);
                    if (Module.PhotonVoice_WebAudioMicIn_InputsStopped) {
                        Module.PhotonVoice_WebAudioMicIn_InputsStopped.delete(handle);
                    }
                     Module.dynCall_viiii(createCallback, handle, 3, 0, 0);
                }
            );

        } else {
            console.error('[PV] PhotonVoice_WebAudioMicIn_Start error: ' + 'getUserMedia not supported on your browser!');
            Module.dynCall_viiii(createCallback, handle, 1, 0, 0);
        }
    },

    PhotonVoice_WebAudioMicIn_Stop: function(handle) { 
        let ctx = Module.PhotonVoice_WebAudioMicIn_Inputs && Module.PhotonVoice_WebAudioMicIn_Inputs.get(handle);
        if (ctx) {
            console.log('[PV] PhotonVoice_WebAudioMicIn_Stop deletes handle ' + handle);
            ctx[0].close();
            window.URL.revokeObjectURL(ctx[1]);
            Module.PhotonVoice_WebAudioMicIn_Inputs.delete(handle);
        } else {
            console.log('[PV] PhotonVoice_WebAudioMicIn_Stop marks handle ' + handle + ' as deleted');
            Module.PhotonVoice_WebAudioMicIn_InputsStopped = Module.PhotonVoice_WebAudioMicIn_InputsStopped || new Map();
            Module.PhotonVoice_WebAudioMicIn_InputsStopped.set(handle, true);
        }
    },
});