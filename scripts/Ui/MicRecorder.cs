// Records the player's microphone for interactive questions. The microphone plays into a muted bus ("QuizMic") with a
// capture effect; its frames are gathered while recording and turned into a small WAV (mono, 16 kHz, 16-bit).
// Needs audio/driver/enable_input in project.godot.

using System;
using System.Collections.Generic;
using Godot;
using QuizNight.Shared;

namespace QuizNight.Ui;

public partial class MicRecorder : Node
{
	const string BusName = "QuizMic";
	const int Rate = 16000;

	AudioEffectCapture? _capture;
	AudioStreamPlayer? _mic;
	readonly List<float> _samples = new();
	float _mixRate = 44100;

	public bool Recording { get; private set; }
	/// <summary>How loud the microphone is right now (0–1, after Settings.MicVolume), so players see it works.</summary>
	public float Level { get; private set; }
	/// <summary>The last moment (msec) the sound hit the top (too loud: it clips).</summary>
	public ulong ClippedAt { get; private set; }
	public double Seconds => _samples.Count / (double)_mixRate;
	/// <summary>Reached the longest a recording may be (QuestionLimits.RecordingMaxSec): it stopped by itself.</summary>
	public event Action? Full;

	static AudioEffectCapture Capture()
	{
		var bus = AudioServer.GetBusIndex(BusName);
		if (bus < 0)
		{
			AudioServer.AddBus();
			bus = AudioServer.BusCount - 1;
			AudioServer.SetBusName(bus, BusName);
			AudioServer.AddBusEffect(bus, new AudioEffectCapture { BufferLength = 0.5f });
			// (players shouldn't hear themselves; the capture effect still gets everything)
			AudioServer.SetBusMute(bus, true);
		}
		return (AudioEffectCapture)AudioServer.GetBusEffect(bus, 0);
	}

	public override void _EnterTree()
	{
		if (DisplayServer.GetName() == "headless") return;
		_mixRate = AudioServer.GetMixRate();
		_capture = Capture();
		_capture.ClearBuffer();
		_mic = new AudioStreamPlayer { Stream = new AudioStreamMicrophone(), Bus = BusName };
		AddChild(_mic);
		_mic.Play();
	}

	public override void _ExitTree()
	{
		_mic?.Stop();
		Recording = false;
	}

	public void Start()
	{
		_samples.Clear();
		_capture?.ClearBuffer();
		Recording = true;
	}

	/// <summary>Stops and returns the recording as a WAV file (null if nothing was recorded).</summary>
	public byte[]? Stop()
	{
		Recording = false;
		return _samples.Count < _mixRate * 0.2 ? null : Wav(_samples, _mixRate);
	}

	public override void _Process(double delta)
	{
		if (_capture == null) return;
		var n = _capture.GetFramesAvailable();
		if (n <= 0)
		{
			Level = Mathf.MoveToward(Level, 0, (float)delta * 2);
			return;
		}
		var frames = _capture.GetBuffer(n);
		var peak = 0f;
		var gain = Storage.Settings.MicGain;
		foreach (var f in frames)
		{
			var mono = (f.X + f.Y) / 2 * gain;
			if (Math.Abs(mono) >= 0.99f) ClippedAt = Time.GetTicksMsec();
			mono = Math.Clamp(mono, -1f, 1f);
			peak = Math.Max(peak, Math.Abs(mono));
			if (Recording) _samples.Add(mono);
		}
		Level = Math.Max(peak, Level - (float)delta * 2);
		if (Recording && Seconds >= QuestionLimits.RecordingMaxSec)
		{
			Recording = false;
			Full?.Invoke();
		}
	}

	/// <summary>Samples (−1…1 at the mix rate) as a mono 16 kHz 16-bit WAV file.</summary>
	public static byte[] Wav(IReadOnlyList<float> samples, float mixRate)
	{
		var count = (int)(samples.Count * Rate / mixRate);
		var data = new byte[count * 2];
		for (var i = 0; i < count; i++)
		{
			// (linear in between the captured samples)
			var at = i * mixRate / Rate;
			var j = (int)at;
			var a = samples[Math.Min(j, samples.Count - 1)];
			var b = samples[Math.Min(j + 1, samples.Count - 1)];
			var v = Math.Clamp(a + (b - a) * (at - j), -1f, 1f);
			BitConverter.TryWriteBytes(data.AsSpan(i * 2), (short)(v * short.MaxValue));
		}
		using var ms = new System.IO.MemoryStream();
		using var w = new System.IO.BinaryWriter(ms);
		w.Write("RIFF"u8);
		w.Write(36 + data.Length);
		w.Write("WAVE"u8);
		w.Write("fmt "u8);
		w.Write(16);
		w.Write((short)1); // PCM
		w.Write((short)1); // mono
		w.Write(Rate);
		w.Write(Rate * 2);
		w.Write((short)2);
		w.Write((short)16);
		w.Write("data"u8);
		w.Write(data.Length);
		w.Write(data);
		w.Flush();
		return ms.ToArray();
	}
}
