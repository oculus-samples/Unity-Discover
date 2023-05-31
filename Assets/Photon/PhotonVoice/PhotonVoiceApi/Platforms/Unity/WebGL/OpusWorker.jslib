mergeInto(LibraryManager.library, {
    
    PhotonVoice_JS_start_worker: function(encoderDataCallback, decoderDataCallback) {
		console.log('[PV] PhotonVoice_JS_start_worker');
        Module.PhotonVoice_JS_Global = {};
		Module.PhotonVoice_JS_Global.encoderChannels = new Map(); // number of channels required to properly allocate Float32Array in PhotonVoice_JS_opus_encode_float()
        Module.PhotonVoice_JS_Global.workerInitialized = false;
        Module.PhotonVoice_JS_Global.workerPreInitQueue = new Array();
        Module.PhotonVoice_JS_Global.opusWorker = new Worker("_opus_worker/opus_worker.js");
        Module.PhotonVoice_JS_Global.opusWorker.onmessage = (e) => {
            if (e.data.error) {
                console.error("[PV] OpusWorker", e.data.error);
            } else if (e.data.workerInitialized) {
                console.log('[PV] OpusWorker initialized');
                Module.PhotonVoice_JS_Global.workerInitialized = true;
//                Module.PhotonVoice_JS_Global.opusWorker.postMessage({op:"test"});

                for (let i = 0; i < Module.PhotonVoice_JS_Global.workerPreInitQueue.length; i++) {
                    Module.PhotonVoice_JS_Global.opusWorker.postMessage(Module.PhotonVoice_JS_Global.workerPreInitQueue[i]);
                }
                Module.PhotonVoice_JS_Global.workerPreInitQueue = new Array();
            } else {
                const b = e.data.packet;
                const ptr = Module._malloc(b.byteLength);
                if (e.data.decode) {
                    HEAPF32.set(b, ptr / 4);
                    Module.dynCall_viiii(decoderDataCallback, e.data.stream, ptr, b.length, e.data.eos);
                } else {
                    HEAPU8.set(b, ptr);
                    Module.dynCall_viii(encoderDataCallback, e.data.stream, ptr, b.length);
                }
                Module._free(ptr);
            }
        };

    },
    

    PhotonVoice_JS_opus_encoder_init: function(st, f, channels, application) {
        console.log('[PV] PhotonVoice_JS_opus_encoder_init', st, f, channels, application);
		Module.PhotonVoice_JS_Global.encoderChannels.set(st, channels);
        const msg = {
                op: 'create_encoder',
                stream: st,
                sampleRate: f,
                numChannels: channels,
                application: application
            };
        if (Module.PhotonVoice_JS_Global.workerInitialized) {
            Module.PhotonVoice_JS_Global.opusWorker.postMessage(msg);
        }
        else {
            Module.PhotonVoice_JS_Global.workerPreInitQueue.push(msg);
        }
        return 0;
    },

    PhotonVoice_JS_opus_encoder_destroy: function(st) {
        console.log('[PV] PhotonVoice_JS_opus_encoder_destroy', st);
		Module.PhotonVoice_JS_Global.encoderChannels.delete(st);
        const msg = {
                op: 'destroy',
                stream: st,
            };
        if (Module.PhotonVoice_JS_Global.workerInitialized) {
            Module.PhotonVoice_JS_Global.opusWorker.postMessage(msg);
        }
        else {
            Module.PhotonVoice_JS_Global.workerPreInitQueue.push(msg);
        }
        return 0;
    },
    
    PhotonVoice_JS_opus_get_version_string: function() {        
        return "OpusWorker";
    },

    PhotonVoice_JS_opus_encode: function(st, pcm, frame_size, data, max_data_bytes) {
        if (!Module.PhotonVoice_JS_Global.workerInitialized) return;
        const msg = {
            op: 'encode',
            stream: st,
            frame: new Int16Array(HEAP16.buffer.slice(pcm, pcm + frame_size * 2 * Module.PhotonVoice_JS_Global.encoderChannels.get(st)))
        };
        Module.PhotonVoice_JS_Global.opusWorker.postMessage(msg);
    },

    PhotonVoice_JS_opus_encode_float: function(st, pcm, frame_size, data, max_data_bytes) {
        if (!Module.PhotonVoice_JS_Global.workerInitialized) return;
        const msg = {
            op: 'encode_float',
            stream: st,
            frame: new Float32Array(HEAPF32.buffer.slice(pcm, pcm + frame_size * 4 * Module.PhotonVoice_JS_Global.encoderChannels.get(st)))
        };
        Module.PhotonVoice_JS_Global.opusWorker.postMessage(msg);
    },

    PhotonVoice_JS_opus_encoder_ctl_set: function(st, request, value) {
        const msg = {
                op: 'ctl_set',
                stream: st,
                request: request,
                value: value
            };
        if (Module.PhotonVoice_JS_Global.workerInitialized) {
            Module.PhotonVoice_JS_Global.opusWorker.postMessage(msg);
        }
        else {
            Module.PhotonVoice_JS_Global.workerPreInitQueue.push(msg);
        }
        return 0;
    },

    // not used, it's not possible to implement synchronously
    PhotonVoice_JS_opus_encoder_ctl_get: function(st, request, value) {
        return 0;
    },

    PhotonVoice_JS_opus_decoder_init: function(st, f, channels) {
        console.log('[PV] PhotonVoice_JS_opus_decoder_init', st, f, channels);
        const msg = {
                op: 'create_decoder',
                stream: st,
                sampleRate: f,
                numChannels: channels,
            };
        if (Module.PhotonVoice_JS_Global.workerInitialized) {
            Module.PhotonVoice_JS_Global.opusWorker.postMessage(msg);
        }
        else {
            Module.PhotonVoice_JS_Global.workerPreInitQueue.push(msg);
        }
        return 0;
    },

    PhotonVoice_JS_opus_decode_async: function(st, data, len, fec, eos) {
        if (!Module.PhotonVoice_JS_Global.workerInitialized) return;
        const msg = {
            op: 'decode',
            stream: st,
            frame: new Int8Array(HEAPF32.buffer.slice(data, data + len)),
			fec: fec,
            eos: eos
        };
        Module.PhotonVoice_JS_Global.opusWorker.postMessage(msg);
    },

    PhotonVoice_JS_opus_decode_float_async: function(st, data, len, fec, eos) {
        if (!Module.PhotonVoice_JS_Global.workerInitialized) return;
        const msg = {
            op: 'decode_float',
            stream: st,
            frame: new Int8Array(HEAPF32.buffer.slice(data, data + len)),
			fec: fec,
            eos: eos
        };
        Module.PhotonVoice_JS_Global.opusWorker.postMessage(msg);
    },
    
    PhotonVoice_JS_opus_decoder_ctl_set: function(st, request, value) {
        const msg = {
                op: 'ctl_set',
                stream: st,
                request: request,
                value: value
            };
        if (Module.PhotonVoice_JS_Global.workerInitialized) {
            Module.PhotonVoice_JS_Global.opusWorker.postMessage(msg);
        }
        else {
            Module.PhotonVoice_JS_Global.workerPreInitQueue.push(msg);
        }
        return 0;
    },
    
    // not used, it's not possible to implement synchronously
    PhotonVoice_JS_opus_decoder_ctl_get: function(st, request, value) {
        return 0;
    },
    
    PhotonVoice_JS_opus_decoder_destroy: function(st) {
        console.log('[PV] PhotonVoice_JS_opus_decoder_destroy', st);
        const msg = {
                op: 'destroy',
                stream: st,
            };
        if (Module.PhotonVoice_JS_Global.workerInitialized) {
            Module.PhotonVoice_JS_Global.opusWorker.postMessage(msg);
        }
        else {
            Module.PhotonVoice_JS_Global.workerPreInitQueue.push(msg);
        }
        return 0;
    }
});