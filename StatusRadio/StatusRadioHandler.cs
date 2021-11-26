// -----------------------------------------------------------------------
// <copyright file="StatusRadioHandler.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using InventorySystem;
using InventorySystem.Items.Radio;
using MEC;
using Mistaken.API;
using Mistaken.API.Diagnostics;
using Mistaken.API.Extensions;
using Mistaken.API.GUI;
using UnityEngine;

namespace Mistaken.StatusRadio
{
    /// <inheritdoc/>
    public class StatusRadioHandler : Module
    {
        /// <inheritdoc cref="Module.Module(Exiled.API.Interfaces.IPlugin{Exiled.API.Interfaces.IConfig})"/>
        public StatusRadioHandler(PluginHandler p)
            : base(p)
        {
        }

        /// <inheritdoc/>
        public override string Name => "StatusRadio";

        /// <inheritdoc/>
        public override void OnEnable()
        {
            this.CallDelayed(0.5f, () =>
            {
                Exiled.Events.Handlers.Player.ChangingRole += this.Player_ChangingRole;
                Exiled.Events.Handlers.Player.ChangingItem += this.Player_ChangingItem;
                Exiled.Events.Handlers.Player.ChangingRadioPreset += this.Player_ChangingRadioPreset;
                Exiled.Events.Handlers.Player.DroppingItem += this.Player_DroppingItem;
                Exiled.Events.Handlers.Player.Handcuffing += this.Player_Handcuffing;
                Exiled.Events.Handlers.Player.Dying += this.Player_Dying;
                Exiled.Events.Handlers.Server.RestartingRound += this.Server_RestartingRound;
            });
        }

        /// <inheritdoc/>
        public override void OnDisable()
        {
            Exiled.Events.Handlers.Player.ChangingRole -= this.Player_ChangingRole;
            Exiled.Events.Handlers.Player.ChangingItem -= this.Player_ChangingItem;
            Exiled.Events.Handlers.Player.ChangingRadioPreset -= this.Player_ChangingRadioPreset;
            Exiled.Events.Handlers.Player.DroppingItem -= this.Player_DroppingItem;
            Exiled.Events.Handlers.Player.Handcuffing -= this.Player_Handcuffing;
            Exiled.Events.Handlers.Player.Dying -= this.Player_Dying;
            Exiled.Events.Handlers.Server.RestartingRound -= this.Server_RestartingRound;
        }

        private readonly Dictionary<uint, string> radioOwners = new Dictionary<uint, string>();

        private void Server_RestartingRound()
        {
            this.radioOwners.Clear();
        }

        private void Player_Dying(Exiled.Events.EventArgs.DyingEventArgs ev)
        {
            if (!ev.IsAllowed)
                return;
            if (ev.Target.HasItem(ItemType.Radio))
                ev.Target.SetGUI("radio", PseudoGUIPosition.MIDDLE, null);
        }

        private void Player_DroppingItem(Exiled.Events.EventArgs.DroppingItemEventArgs ev)
        {
            if (!ev.IsAllowed)
                return;

            if (ev.Item.Type != ItemType.Radio)
                return;

            if (ev.Player.CurrentItem.Serial == ev.Item.Serial)
                ev.Player.SetGUI("radio", PseudoGUIPosition.MIDDLE, null);
        }

        private void Player_Handcuffing(Exiled.Events.EventArgs.HandcuffingEventArgs ev)
        {
            if (!ev.IsAllowed)
                return;
            if (ev.Target.HasItem(ItemType.Radio))
                ev.Target.SetGUI("radio", PseudoGUIPosition.MIDDLE, null);
        }

        private void Player_ChangingRadioPreset(Exiled.Events.EventArgs.ChangingRadioPresetEventArgs ev)
        {
            if (!ev.IsAllowed)
                return;
            if (ev.Player.CurrentItem.Type != ItemType.Radio)
                return;
            this.CallDelayed(0.1f, () =>
            {
                if (!ev.IsAllowed)
                    return;
                if (ev.NewValue == 0)
                    ev.Player.SetGUI("radio", PseudoGUIPosition.MIDDLE, null);
                else
                    ev.Player.SetGUI("radio", PseudoGUIPosition.MIDDLE, this.GetDisplay());
            });
        }

        private void Player_ChangingItem(Exiled.Events.EventArgs.ChangingItemEventArgs ev)
        {
            if (ev.NewItem.Type == ItemType.Radio)
                Timing.RunCoroutine(this.UpdateGUI(ev.Player));
            else
                ev.Player.SetGUI("radio", PseudoGUIPosition.MIDDLE, null);
        }

        private IEnumerator<float> UpdateGUI(Player player)
        {
            yield return Timing.WaitForSeconds(0.5f);
            do
            {
                this.SingleUpdateGUI(player);
                yield return Timing.WaitForSeconds(5);
            }
            while (player.IsAlive && player.CurrentItem?.Type == ItemType.Radio);
            player.SetGUI("radio", PseudoGUIPosition.MIDDLE, null);
        }

        private void SingleUpdateGUI(Player player)
        {
            if (player.CurrentItem is Exiled.API.Features.Items.Radio radio && radio.Range != Exiled.API.Enums.RadioRange.Disabled)
                player.SetGUI("radio", PseudoGUIPosition.MIDDLE, this.GetDisplay());
            else
                player.SetGUI("radio", PseudoGUIPosition.MIDDLE, null);
        }

        private void Player_ChangingRole(Exiled.Events.EventArgs.ChangingRoleEventArgs ev)
        {
            MEC.Timing.CallDelayed(1, () =>
            {
                if (!ev.Player.HasItem(ItemType.Radio))
                    return;
                var serial = ev.Player.Items.First(x => x.Type == ItemType.Radio).Serial;
                this.radioOwners[serial] = $"[{ev.Player.UnitName}] {ev.Player.GetDisplayName()}";
            });
        }

        private string GetDisplay()
        {
            Dictionary<uint, string> toWrite = new Dictionary<uint, string>();
            List<string> active = new List<string>();
            List<string> notActive = new List<string>();
            foreach (var player in RealPlayers.List.Where(x => x.HasItem(ItemType.Radio)))
            {
                var item = player.Items.First(x => x.Type == ItemType.Radio) as Exiled.API.Features.Items.Radio;

                uint serial = item.Serial;
                if (!this.radioOwners.ContainsKey(serial))
                    this.radioOwners[serial] = "[UnKnOWn] " + this.GetNoise(serial);
                if (item.Range == Exiled.API.Enums.RadioRange.Disabled)
                    toWrite[serial] = $"<color=#d4d4d4>{this.radioOwners[serial]}</color>";
                else
                    toWrite[serial] = $"<color=green>{this.radioOwners[serial]}</color>";
            }

            foreach (var item in Map.Pickups.Where(x => x.Type == ItemType.Radio).Select(x => x.Base as RadioPickup))
            {
                if (!this.radioOwners.ContainsKey(item.Info.Serial))
                    this.radioOwners[item.Info.Serial] = "[UnKnOWn] " + this.GetNoise(item.Info.Serial);
                if (item.SavedEnabled)
                    toWrite[item.Info.Serial] = $"<color=#d4d4d4>{this.radioOwners[item.Info.Serial]}</color>";
                else
                    toWrite[item.Info.Serial] = $"<color=red>{this.radioOwners[item.Info.Serial]}</color>";
            }

            List<string> list1 = new List<string>();
            List<string> list2 = new List<string>();
            List<string> list3 = new List<string>();

            foreach (var item in toWrite.OrderBy(x => x.Key).Select(x => x.Value))
            {
                if (list1.Count > 24)
                {
                    if (list2.Count > 24)
                    {
                        if (list3.Count > 24)
                            continue;
                        else
                            list3.Add(item);
                    }
                    else
                        list2.Add(item);
                }
                else
                    list1.Add(item);
            }

            int i = 0;
            List<string> tor = new List<string>();
            while (i < 25)
            {
                string column1 = list1.Count > i ? list1[i] : string.Empty;
                string column2 = list2.Count > i ? list2[i] : string.Empty;
                string column3 = list3.Count > i ? list3[i] : string.Empty;
                tor.Add($"<align=left>{column1}</align><line-height=1px><br></line-height><align=center>{column2}</align><line-height=1px><br></line-height><align=right>{column3}</align>");
                i++;
            }

            return "<align=left><size=50%>Radio Status:<br>" + string.Join("<br>", toWrite.OrderBy(x => x.Key).Select(x => x.Value)) + "</size></align>";
        }

        private string GetNoise(uint seed)
        {
            string tmp = "ABCDEFGHIJKLMNOPQRSTWXYZ1234567890qwertyuiopasdfghjklzxcvbnm";
            var rng = new System.Random((int)seed);
            string tor = string.Empty;
            for (int i = 0; i < 8; i++)
                tor += tmp[rng.Next(0, tmp.Length)];
            return tor;
        }
    }
}
