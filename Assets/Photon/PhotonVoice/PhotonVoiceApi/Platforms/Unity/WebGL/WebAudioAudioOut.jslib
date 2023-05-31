mergeInto(LibraryManager.library, {
	
	// Safari:
	PhotonVoice_WebAudioAudioOut_ResumeAudioContext: function() {
		if (Module.PhotonVoice_WebAudioAudioOut_Global) {
			Module.PhotonVoice_WebAudioAudioOut_Global.ResumeAudioContext();
		}
	},
	
    PhotonVoice_WebAudioAudioOut_Start: function(handle, sampleRate, channels, bufferSamples, spatialBlend) {
		if (!Module.PhotonVoice_WebAudioAudioOut_Global) {
			Module.PhotonVoice_WebAudioAudioOut_Global = {};
			Module.PhotonVoice_WebAudioAudioOut_Global.Sources = new Map();
			
			// Safari:
			Module.PhotonVoice_WebAudioAudioOut_Global.ResumeAudioContext = function() {
				if (Module.PhotonVoice_WebAudioAudioOut_Global) {
					for (const s of Module.PhotonVoice_WebAudioAudioOut_Global.Sources.values()) {
						if (s.audioContext.state == 'suspended' || s.audioContext.state == 'interrupted') {
							console.log('[PV] PhotonVoice_WebAudioAudioOut_Global.ResumeAudioContext resume', s.audioContext, s.audioContext.state);
							s.audioContext.resume().then(() => {
								console.log('[PV] PhotonVoice_WebAudioAudioOut_Global.ResumeAudioContext resumed', s.audioContext, s.audioContext.state);
							});
						}
					}
				}
			}
			// Safari: resuming mic's audio context ui handler
			// window.addEventListener('mousedown', Module.PhotonVoice_WebAudioAudioOut_Global.ResumeAudioContext);
			// window.addEventListener('touchstart', Module.PhotonVoice_WebAudioAudioOut_Global.ResumeAudioContext);
			
			const playWorkerFoo = function() {
				var buffers; // array of ring buffers per channels
				var bufferLen;
				var channels;
				class PlayProcessor extends AudioWorkletProcessor {
					constructor(options) {
						super(options);
						channels = options.processorOptions.channels;

						bufferLen = options.processorOptions.bufferSamples;
						buffers = new Array(channels);
						for (let ch = 0; ch < channels; ch++)
							buffers[ch] = new Float32Array(bufferLen);
							
						// interlaced frame samples and offset in parameters
						this.port.onmessage = (e) => {
							const data = e.data[0];
							const offset = e.data[1];
							const excess = offset + data.length / channels - bufferLen;
							if (channels == 1) {
								if (excess > 0) {
									const len1 = bufferLen - offset;
									buffers[0].set(data.subarray(0, len1), offset);
									buffers[0].set(data.subarray(len1, len1 + excess), 0);
								} else {
									buffers[0].set(data, offset);
								}
							} else {
								// deinterlace
								for (let ch = 0; ch < channels; ch++) {
									let src = ch;
									let dst = offset;
									const b = buffers[ch];
									
									if (excess > 0) {
										for (; dst < bufferLen; dst++, src += channels) {
											b[dst] = data[src];
										}
										dst = 0; // go to the beginning of the buffer
									}
									for (; src < data.length; dst++, src += channels) {
										b[dst] = data[src];
									}
								}
							}
						};
					}
					
					process(inputs, outputs, parameters) {
						if (inputs[0].length == 0) // happens once after source stops
							return;
						const bufPos = inputs[0][0][0]; // we filled the ring buffer with positions during initialization
						const outLen = outputs[0][0].length;
						
						const excess = bufPos + outLen - bufferLen;
						const len1 = bufferLen - bufPos;

						for (let ch = 0; ch < channels; ch++) {
							if (excess > 0) {
								outputs[0][ch].set(buffers[ch].subarray(bufPos, bufPos + len1), 0);
								outputs[0][ch].set(buffers[ch].subarray(0, excess), len1);
							} else {
								outputs[0][ch].set(buffers[ch].subarray(bufPos, bufPos + outLen), 0);
							}
						}

						this.port.postMessage(excess > 0 ? excess : bufPos + outLen);
						
						return true;
					}
				}
				
				registerProcessor('photon-voice-play-processor', PlayProcessor);
			}

			let ws = playWorkerFoo.toString();
			ws = ws.substring(ws.indexOf("{") + 1, ws.lastIndexOf("}"));
			const blob = new Blob([ws], {
				type: "text/javascript"
			});

			
			Module.PhotonVoice_WebAudioAudioOut_Global.PlayWorkerURL = window.URL.createObjectURL(blob);
		}
		// end of Module.PhotonVoice_WebAudioAudioOut_Global creation
		
		
		const audioContext = new AudioContext({
            sampleRate: sampleRate
        });
		
		const addProc = function(audioContext) {
			const playProc = new AudioWorkletNode(audioContext, 'photon-voice-play-processor', {processorOptions : {channels: channels, bufferSamples: bufferSamples}});
			const gainNode = audioContext.createGain();
			let pannerNode;
			let spatialBlendNode1;
			let spatialBlendNode2;
			if (spatialBlend > 0) {
				const options = {
				}
				pannerNode = new PannerNode(audioContext, options);
				
				if (spatialBlend < 1) {
					spatialBlendNode1 = audioContext.createGain();
					spatialBlendNode2 = audioContext.createGain();
					
					spatialBlendNode1.gain.value = spatialBlend;
					spatialBlendNode2.gain.value = 1 - spatialBlend;

					playProc.connect(gainNode).connect(pannerNode).connect(spatialBlendNode1).connect(audioContext.destination);
					gainNode.connect(spatialBlendNode2).connect(audioContext.destination);
					
					console.log('[PV] PhotonVoice_WebAudioAudioOut_Start: creating 3D player with dynamic spatial blend');
				} else {
					playProc.connect(gainNode).connect(pannerNode).connect(audioContext.destination);
					
					console.log('[PV] PhotonVoice_WebAudioAudioOut_Start: creating 3D player');
				}
			} else {                
				playProc.connect(gainNode).connect(audioContext.destination);
				console.log('[PV] PhotonVoice_WebAudioAudioOut_Start: creating 2D player');
			}

			let ctx = {
				audioContext: audioContext, 
				playProc: playProc,
				playPos: 0,
				gainNode: gainNode,
				pannerNode: pannerNode,
				spatialBlendNode1: spatialBlendNode1,
				spatialBlendNode2: spatialBlendNode2
			}
			
			Module.PhotonVoice_WebAudioAudioOut_Global.Sources.set(handle, ctx);
							
			playProc.port.onmessage = function(e) {
				// store ring buffer position sent from process()
				ctx.playPos = e.data;
			}
							
			// create AudioBuffer and AudioBufferSourceNode for the play processor input
			const audioBuffer = audioContext.createBuffer(channels, bufferSamples, audioContext.sampleRate);
			const buf = audioBuffer.getChannelData(0);
			// write buffer position to the 0 channel buffer
			for (let i = 0; i < buf.length; i++) {
				buf[i] = i;
			}
			
			ctx.source = audioContext.createBufferSource();
			ctx.source.buffer = audioBuffer;
			ctx.source.connect(playProc);
			ctx.source.loop = true;
			ctx.source.start();

			audioContext.resume();
		};
		
		audioContext.audioWorklet.addModule(Module.PhotonVoice_WebAudioAudioOut_Global.PlayWorkerURL).then(
			function() {
				addProc(audioContext);
				console.log('[PV] PhotonVoice_WebAudioAudioOut_Start: audioContext.audioWorklet.addModule() OK');
			},
			function(x) {
				console.error('[PV] PhotonVoice_WebAudioAudioOut_Start error: audioContext.audioWorklet.addModule():', x);
				res = 1;
			}
		)	

        return 0;
    },


    PhotonVoice_WebAudioAudioOut_GetOutPos: function(handle) {
        const ctx = Module.PhotonVoice_WebAudioAudioOut_Global.Sources.get(handle);
        if (ctx) {
            return ctx.playPos;
        }
    },

    PhotonVoice_WebAudioAudioOut_Write: function(handle, data, dataLenFloat, offsetSamples) {
        const ctx = Module.PhotonVoice_WebAudioAudioOut_Global.Sources.get(handle);
        if (ctx) {
            const x = HEAPF32.slice(data / 4, data / 4 + dataLenFloat);
            ctx.playProc.port.postMessage([x, offsetSamples]);
        }
    },

    PhotonVoice_WebAudioAudioOut_SetVolume: function(handle, x) {
        const ctx = Module.PhotonVoice_WebAudioAudioOut_Global.Sources.get(handle);
        if (ctx) {
            ctx.gainNode.gain.value = x; // is enough but produces clicks
            // smooth transition throws in FireFix: NotSupportedError: AudioParam.setValueCurveAtTime: Can't add events during a curve event
            //ctx.gainNode.gain.cancelScheduledValues(ctx.audioContext.currentTime);
            //ctx.gainNode.gain.setValueCurveAtTime([ctx.gainNode.gain.value, x], ctx.audioContext.currentTime, 0.01);
            return 0;
        } else {
            return 1;
        }
    },

    PhotonVoice_WebAudioAudioOut_SetSpatialBlend: function(handle, x) {
        const ctx = Module.PhotonVoice_WebAudioAudioOut_Global.Sources.get(handle);
        if (ctx && ctx.spatialBlendNode1) {
            ctx.spatialBlendNode1.gain.value = x;
            ctx.spatialBlendNode2.gain.value = 1 - x;
            return 0;
        } else {
            return 1;
        }
    },
    
    PhotonVoice_WebAudioAudioOut_SetListenerPosition: function(handle, x, y, z) {
        const ctx = Module.PhotonVoice_WebAudioAudioOut_Global.Sources.get(handle);
        if (ctx) {
            ctx.audioContext.listener.setPosition(x, y, z);
            return 0;
        } else {
            return 1;
        }
    },
    
    PhotonVoice_WebAudioAudioOut_SetListenerOrientation: function(handle, fx, fy, fz, ux, uy, uz) {
        const ctx = Module.PhotonVoice_WebAudioAudioOut_Global.Sources.get(handle);
        if (ctx) {
            ctx.audioContext.listener.setOrientation(fx, fy, fz, ux, uy, uz);
            return 0;
        } else {
            return 1;
        }
    },
    
    PhotonVoice_WebAudioAudioOut_SetPosition: function(handle, x, y, z) {
        const ctx = Module.PhotonVoice_WebAudioAudioOut_Global.Sources.get(handle);
        if (ctx && ctx.pannerNode) {
            ctx.pannerNode.setPosition(x, y, z);
            return 0;
        } else {
            return 1;
        }
    },
    
    PhotonVoice_WebAudioAudioOut_Stop: function(handle) {
        const ctx = Module.PhotonVoice_WebAudioAudioOut_Global.Sources.get(handle);
        if (ctx) {
            ctx.audioContext.close().then(() => {
				console.log('[PV] PhotonVoice_WebAudioAudioOut_Stop audioContext closed', handle);
			});
            Module.PhotonVoice_WebAudioAudioOut_Global.Sources.delete(handle);
            console.log('[PV] PhotonVoice_WebAudioAudioOut_Stop deletes handle', handle);
        }
    },
});