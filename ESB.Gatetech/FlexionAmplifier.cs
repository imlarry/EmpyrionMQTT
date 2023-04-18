using System;
using Eleon.Modding;
using Newtonsoft.Json.Linq;
using UnityEngine;
using ESBGameMod;

namespace Gatetech
{
    public class FlexionAmplifier
    {
        private readonly ContextData _ctx;

        public FlexionAmplifier(ContextData ctx)
        {
            _ctx = ctx;
        }
        
        // Charge .. Startes a two minute process of charging the capacitory relay with incremental consumption of pentaxid.
        public async void Charge(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                var prop = "mocked";
                JObject json = new JObject(new JProperty("SomeProperty", prop));
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        // Summon .. Moves an amplifier to a location within the current playfield, no charge cycle required.
        public async void Summon(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                int entityId = args.GetValue("EntityId").Value<int>();
                var amplifierEntity = _ctx.ModApi.ClientPlayfield.Entities[entityId];    // this mechanism to get a playfield handle won't work for PFserver
                if (amplifierEntity != null)
                {
                    // confirm this is an amplifier before proceeding
                    // move it
                }

                JObject json = new JObject(new JProperty("EntityId", entityId));
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        // SiteToSite .. Moves an amplifier and in range entites via warp within the current playfield, no charge cycle required.
        public async void SiteToSite(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                var amplifierEntityId = "mocked";
                // get entity list via playfield
                // use the Entity.Pos of FA as (0,0,0)
                // compute distance to other entities and add to list if <= 30 meters

                JObject json = new JObject(new JProperty("SomeProperty", amplifierEntityId));
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        // Trigger .. Activates paired amplifiers to create entangled transdimentional warp bubbles around entities in range
        // and initiate bidirectional entity transfer. Triggering before full charge is reached results in a loss of pentaxid
        // dedicated to the jump. 
        public async void Trigger(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                var prop = "mocked";
                JObject json = new JObject(new JProperty("SomeProperty", prop));
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

        // Implode .. Destroys either a single unpaired amplifier or a paired set after the charge cycle completes.
        public async void Implode(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                var prop = "mocked";
                JObject json = new JObject(new JProperty("SomeProperty", prop));
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "R"), json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(_ctx.Messenger.RespondTo(topic, "X"), ex.Message);
            }
        }

    }

}

