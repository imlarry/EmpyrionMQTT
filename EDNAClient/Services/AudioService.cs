using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using EDNAClient.Core;

namespace EDNAClient.Services
{
    // Singleton audio facade for alerts, sound effects, and TTS.
    // Initialized once from App.OnStartup on the UI thread (MediaPlayer has
    // thread affinity to the dispatcher that created it).
    //
    // Alert files are looked up by short name under AudioDirectory; the
    // glob in EDNA.csproj copies *.wav and *.mp3 from EDNAClient/Resources/Audio/
    // to the deployed Resources/Audio/ folder. When a named alert file is
    // missing, PlayAlert falls back to System.Media.SystemSounds.Asterisk so
    // callers can be wired before assets ship -- see Docs/OpenIssues.md.
    public sealed class AudioService : IDisposable
    {
        public static AudioService? Instance { get; private set; }

        public static readonly string AudioDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Resources", "Audio");

        private const int PoolSize = 8;
        private readonly MediaPlayer[] _pool;
        private int _next;
        private readonly Dispatcher _dispatcher;

        private readonly SpeechSynthesizer _tts;
        private readonly object _ttsLock = new object();

        private AudioService()
        {
            _dispatcher = Application.Current.Dispatcher;
            _pool = new MediaPlayer[PoolSize];
            for (int i = 0; i < PoolSize; i++) _pool[i] = new MediaPlayer();

            _tts = new SpeechSynthesizer();
            _tts.SetOutputToDefaultAudioDevice();
            SelectFemaleVoice(_tts);
        }

        // Prefer a female voice for EDNA. Falls back silently to the system
        // default if no female voice is installed. Logs the chosen voice and
        // the full installed-voice list so you can switch to a specific one
        // later via SelectVoice(name).
        private static void SelectFemaleVoice(SpeechSynthesizer tts)
        {
            try
            {
                var installed = tts.GetInstalledVoices();
                EdnaLogger.Log("AudioService: installed TTS voices: " +
                    string.Join(", ", installed.Select(v => v.VoiceInfo.Name + " (" + v.VoiceInfo.Gender + "/" + v.VoiceInfo.Culture + ")")));

                tts.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult);
                EdnaLogger.Log("AudioService: selected voice = " + tts.Voice.Name + " (" + tts.Voice.Gender + ")");
            }
            catch (Exception ex)
            {
                EdnaLogger.Warn($"AudioService: female voice select failed; using system default. {ex.GetType().Name}: {ex.Message}");
            }
        }

        public static void Initialize()
        {
            if (Instance != null) return;
            Instance = new AudioService();
            EdnaLogger.Log($"AudioService initialized (audio dir: {AudioDirectory})");
        }

        // Play a named alert from {AudioDirectory}/{name}.wav (or .mp3).
        // Falls back to the system Asterisk sound when no matching file exists.
        public void PlayAlert(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            string wav = Path.Combine(AudioDirectory, name + ".wav");
            if (File.Exists(wav)) { PlaySfx(wav); return; }

            string mp3 = Path.Combine(AudioDirectory, name + ".mp3");
            if (File.Exists(mp3)) { PlaySfx(mp3); return; }

            EdnaLogger.Warn($"AudioService: alert '{name}' not found in {AudioDirectory}; using SystemSounds.Asterisk");
            try { SystemSounds.Asterisk.Play(); } catch { }
        }

        // Play an arbitrary audio file. Uses a round-robin MediaPlayer pool so
        // overlapping callers do not cut each other off.
        public void PlaySfx(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                EdnaLogger.Warn($"AudioService.PlaySfx: file not found: {filePath}");
                return;
            }

            _dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    int slot = _next;
                    _next = (_next + 1) % PoolSize;
                    var player = _pool[slot];
                    player.Stop();
                    player.Open(new Uri(filePath, UriKind.Absolute));
                    player.Play();
                }
                catch (Exception ex)
                {
                    EdnaLogger.Warn($"AudioService.PlaySfx failed for '{filePath}': {ex.Message}");
                }
            }));
        }

        // Speak text. SpeechSynthesizer queues utterances natively; passing
        // priority=true cancels everything pending and speaks immediately.
        public void Speak(string text, bool priority = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                lock (_ttsLock)
                {
                    if (priority) _tts.SpeakAsyncCancelAll();
                    _tts.SpeakAsync(text);
                }
            }
            catch (Exception ex)
            {
                EdnaLogger.Warn($"AudioService.Speak failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try { lock (_ttsLock) { _tts.SpeakAsyncCancelAll(); _tts.Dispose(); } } catch { }
            try
            {
                _dispatcher.Invoke(() =>
                {
                    foreach (var p in _pool)
                    {
                        try { p.Stop(); p.Close(); } catch { }
                    }
                });
            }
            catch { }
            Instance = null;
        }
    }
}
