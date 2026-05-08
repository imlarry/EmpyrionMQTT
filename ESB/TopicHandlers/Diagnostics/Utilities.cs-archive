using Eleon.Modding;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers.Diagnostics
{
    public class Utilities
    {
        private readonly ContextData _ctx;

        public Utilities(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V2.Utilities.DumpMemory",  DumpMemory);
            _ctx.Messenger.RegisterHandler("V2.Utilities.WindowInfo",  WindowInfo);
            _ctx.Messenger.RegisterHandler("V2.Utilities.TraceEntity", TraceEntity);
        }

        public async Task DumpMemory(string topic, string payload)
        {
            try
            {
                string processName = Process.GetCurrentProcess().ProcessName;
                string dumpFileName = DumpProcess(processName);
                JObject json = new JObject(new JProperty("DumpFileName", dumpFileName));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task WindowInfo(string topic, string payload)
        {
            try
            {
                JObject json = GetWindowInfo();
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task TraceEntity(string topic, string payload)
        {
            try
            {
                JObject applicationArgs = JObject.Parse(payload);
                int entityId   = Convert.ToInt32(applicationArgs.GetValue("EntityId"));
                int duration    = Convert.ToInt32(applicationArgs.GetValue("Duration"));
                int refreshRate = Convert.ToInt32(applicationArgs.GetValue("RefreshRate"));

                // Start a background trace loop - position reads happen on the main thread
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var pfInit = _ctx.ModApi.ClientPlayfield;
                        IEntity entInit;
                        if (pfInit == null || !pfInit.Entities.TryGetValue(entityId, out entInit) || entInit == null)
                        {
                            await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson($"Entity {entityId} not found in ClientPlayfield.Entities"));
                            return;
                        }

                        DateTime startTime = DateTime.Now;
                        while ((DateTime.Now - startTime).TotalSeconds < duration)
                        {
                            // Re-check each tick - entity may have been killed/unloaded
                            var pfTick = _ctx.ModApi.ClientPlayfield;
                            IEntity entity;
                            if (pfTick != null) pfTick.Entities.TryGetValue(entityId, out entity);
                            else entity = null;
                            if (entity == null)
                            {
                                await _ctx.Messenger.SendAsync(MessageClass.Event, topic,
                                    new JObject(new JProperty("Status", "EntityLost"), new JProperty("EntityId", entityId)).ToString(Formatting.None));
                                return;
                            }

                            // Read position on main thread - Unity objects require it
                            Vector3 position = default;
                            await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                            {
                                position = entity.Position;
                                await Task.CompletedTask;
                            });

                            JObject json = new JObject(
                                new JProperty("EntityId", entityId),
                                new JProperty("Position", new JObject(
                                    new JProperty("X", position.x),
                                    new JProperty("Y", position.y),
                                    new JProperty("Z", position.z)
                                ))
                            );
                            await _ctx.Messenger.SendAsync(MessageClass.Event, topic, json.ToString(Formatting.None));

                            await Task.Delay(TimeSpan.FromSeconds(refreshRate));
                        }

                        await _ctx.Messenger.SendAsync(MessageClass.Information, topic,
                            new JObject(new JProperty("Status", "TraceExpired"), new JProperty("EntityId", entityId)).ToString(Formatting.None));
                    }
                    catch (Exception ex)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
                    }
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        // -------------------------------------------------------------------------
        // MemoryDumper
        // -------------------------------------------------------------------------
        private static string DumpProcess(string processName)
        {
            var process = Process.GetProcessesByName(processName).FirstOrDefault();
            if (process == null)
            {
                return null;
            }

            string dumpFileName = $"{processName}_{DateTime.Now:yyyyMMdd_HHmmss}.dmp";
            Process.Start("C:\\Users\\imlar\\OneDrive\\Desktop\\Procdump\\procdump64", $"-ma -accepteula {process.Id} {dumpFileName}");
            return dumpFileName;
        }

        // -------------------------------------------------------------------------
        // WinInfo
        // -------------------------------------------------------------------------
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private static JObject GetWindowInfo()
        {
            var hWnd = Process.GetCurrentProcess().MainWindowHandle;
            if (hWnd == IntPtr.Zero)
            {
                return new JObject();
            }

            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);

            GetWindowRect(hWnd, out var r);
            return new JObject(
                new JProperty("Name",   sb.ToString()),
                new JProperty("Left",   r.Left),
                new JProperty("Top",    r.Top),
                new JProperty("Right",  r.Right),
                new JProperty("Bottom", r.Bottom),
                new JProperty("Width",  r.Right - r.Left),
                new JProperty("Height", r.Bottom - r.Top)
            );
        }
    }
}
