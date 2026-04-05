// Audio Worklet for processing microphone audio data
// This runs in a separate audio thread for better performance

class AudioProcessorWorklet extends AudioWorkletProcessor {
    constructor() {
        super();
        this.sampleRate = 16000;  // Target sample rate
        this.accumulatedSamples = [];
        this.samplesPerChunk = 8000;  // 500ms @ 16kHz = 8000 samples
    }

    // Message handling from main thread
    process(inputs, outputs, parameters) {
        const input = inputs[0];
        const inputChannel = input[0];

        if (!inputChannel) {
            return true;  // No input, continue processing
        }

        // Get the current sample rate
        const currentSampleRate = this.port.context?.sampleRate || 48000;

        // Accumulate samples
        for (let i = 0; i < inputChannel.length; i++) {
            this.accumulatedSamples.push(inputChannel[i]);
        }

        // When we have enough samples, send to main thread
        if (this.accumulatedSamples.length >= this.samplesPerChunk) {
            // Resample to target rate if needed
            const samplesToSend = this.resampleAudio(
                this.accumulatedSamples,
                currentSampleRate,
                this.sampleRate
            );

            // Convert to 16-bit PCM
            const pcmData = this.floatToPcm16(samplesToSend);

            // Send to main thread
            if (pcmData.length > 0) {
                this.port.postMessage({
                    type: 'audioData',
                    data: pcmData.buffer,
                    samples: samplesToSend.length,
                    originalSamples: this.accumulatedSamples.length,
                    fromSampleRate: currentSampleRate,
                    toSampleRate: this.sampleRate
                }, [pcmData.buffer]);
            }

            // Clear accumulator
            this.accumulatedSamples = [];
        }

        return true;  // Keep the processor alive
    }

    // Resample audio from one rate to another
    resampleAudio(samples, fromRate, toRate) {
        if (fromRate === toRate) {
            return samples;
        }

        const ratio = fromRate / toRate;
        const outputLength = Math.floor(samples.length / ratio);
        const result = new Float32Array(outputLength);

        for (let i = 0; i < outputLength; i++) {
            const sourceIndex = Math.floor(i * ratio);
            result[i] = samples[sourceIndex];
        }

        return result;
    }

    // Convert float samples to 16-bit PCM
    floatToPcm16(samples) {
        const pcm = new Int16Array(samples.length);
        for (let i = 0; i < samples.length; i++) {
            // Clamp to -1 to 1 range
            const clamped = Math.max(-1, Math.min(1, samples[i]));
            // Convert to 16-bit PCM
            pcm[i] = Math.round(clamped * 32767);
        }
        return pcm;
    }
}

registerProcessor('audio-processor', AudioProcessorWorklet);