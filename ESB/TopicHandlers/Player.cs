using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers
{
    public class Player : IPlayer
    {
        private readonly ContextData _ctx;

        public Player(ContextData ctx)
        {
            _ctx = ctx;
        }

        public async Task Subscribe()
        {
            await _ctx.Messenger.SubscribeAsync("Player.Teleport", Teleport);
            await _ctx.Messenger.SubscribeAsync("Player.SteamId", SteamId);
        }

        public async Task Teleport(string topic, string payload)
        {
            bool teleport;
            JObject PlayerArgs = JObject.Parse(payload);

            string playfield = PlayerArgs.GetValue("Playfield")?.ToString();
            string posStr = PlayerArgs.GetValue("Pos")?.ToString();
            string rotStr = PlayerArgs.GetValue("Rot")?.ToString();

            if (string.IsNullOrEmpty(posStr))
            {
                throw new ArgumentException("Pos argument is required");
            }

            string[] pvalues = posStr.Split(',');
            Vector3 pos = new Vector3(float.Parse(pvalues[0]), float.Parse(pvalues[1]), float.Parse(pvalues[2]));

            if (playfield == null)
            {
                teleport = _ctx.ModApi.Application.LocalPlayer.Teleport(pos);
            }
            else
            {
                if (string.IsNullOrEmpty(rotStr))
                {
                    throw new ArgumentException("Rot argument is required when Playfield is provided");
                }

                string[] rvalues = rotStr.Split(',');
                Vector3 rot = new Vector3(float.Parse(rvalues[0]), float.Parse(rvalues[1]), float.Parse(rvalues[2]));

                teleport = _ctx.ModApi.Application.LocalPlayer.Teleport(playfield, pos, rot);
            }

            JObject json = new JObject(new JProperty("Teleport", teleport));
            await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
        }
        public async Task SteamId(string topic, string payload)
        {
            var steamId = _ctx.ModApi.Application.LocalPlayer.SteamId;
            JObject json = new JObject(new JProperty("SteamId", steamId));
            await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
        }
    }
}