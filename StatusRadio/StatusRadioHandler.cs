// -----------------------------------------------------------------------
// <copyright file="StatusRadioHandler.cs" company="Mistaken">
// Copyright (c) Mistaken. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
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
                Exiled.Events.Handlers.Player.ChangedRole += this.Handle<Exiled.Events.EventArgs.ChangedRoleEventArgs>((ev) => this.Player_ChangedRole(ev));
                Exiled.Events.Handlers.Player.ChangingItem += this.Handle<Exiled.Events.EventArgs.ChangingItemEventArgs>((ev) => this.Player_ChangingItem(ev));
                Exiled.Events.Handlers.Player.ChangingRadioPreset += this.Handle<Exiled.Events.EventArgs.ChangingRadioPresetEventArgs>((ev) => this.Player_ChangingRadioPreset(ev));
                Exiled.Events.Handlers.Player.DroppingItem += this.Handle<Exiled.Events.EventArgs.DroppingItemEventArgs>((ev) => this.Player_DroppingItem(ev));
                Exiled.Events.Handlers.Player.ItemDropped += this.Handle<Exiled.Events.EventArgs.ItemDroppedEventArgs>((ev) => this.Player_ItemDropped(ev));
                Exiled.Events.Handlers.Player.PickingUpItem += this.Handle<Exiled.Events.EventArgs.PickingUpItemEventArgs>((ev) => this.Player_PickingUpItem(ev));
                Exiled.Events.Handlers.Player.Handcuffing += this.Handle<Exiled.Events.EventArgs.HandcuffingEventArgs>((ev) => this.Player_Handcuffing(ev));
                Exiled.Events.Handlers.Player.Dying += this.Handle<Exiled.Events.EventArgs.DyingEventArgs>((ev) => this.Player_Dying(ev));
                Exiled.Events.Handlers.Server.RestartingRound += this.Handle(() => this.Server_RestartingRound(), "RestartingRound");
            });
        }

        /// <inheritdoc/>
        public override void OnDisable()
        {
            Exiled.Events.Handlers.Player.ChangedRole -= this.Handle<Exiled.Events.EventArgs.ChangedRoleEventArgs>((ev) => this.Player_ChangedRole(ev));
            Exiled.Events.Handlers.Player.ChangingItem -= this.Handle<Exiled.Events.EventArgs.ChangingItemEventArgs>((ev) => this.Player_ChangingItem(ev));
            Exiled.Events.Handlers.Player.ChangingRadioPreset -= this.Handle<Exiled.Events.EventArgs.ChangingRadioPresetEventArgs>((ev) => this.Player_ChangingRadioPreset(ev));
            Exiled.Events.Handlers.Player.DroppingItem -= this.Handle<Exiled.Events.EventArgs.DroppingItemEventArgs>((ev) => this.Player_DroppingItem(ev));
            Exiled.Events.Handlers.Player.ItemDropped -= this.Handle<Exiled.Events.EventArgs.ItemDroppedEventArgs>((ev) => this.Player_ItemDropped(ev));
            Exiled.Events.Handlers.Player.PickingUpItem -= this.Handle<Exiled.Events.EventArgs.PickingUpItemEventArgs>((ev) => this.Player_PickingUpItem(ev));
            Exiled.Events.Handlers.Player.Handcuffing -= this.Handle<Exiled.Events.EventArgs.HandcuffingEventArgs>((ev) => this.Player_Handcuffing(ev));
            Exiled.Events.Handlers.Player.Dying -= this.Handle<Exiled.Events.EventArgs.DyingEventArgs>((ev) => this.Player_Dying(ev));
            Exiled.Events.Handlers.Server.RestartingRound -= this.Handle(() => this.Server_RestartingRound(), "RestartingRound");
        }

        private readonly Dictionary<uint, string> radioOwners = new Dictionary<uint, string>();
        private readonly Dictionary<Player, Radio> playerRadioDictionary = new Dictionary<Player, Radio>();
        private readonly HashSet<uint> disabledFloorRadios = new HashSet<uint>();
        private readonly Dictionary<Pickup, uint> radioIds = new Dictionary<Pickup, uint>();
        private readonly Dictionary<Player, Dictionary<int, uint>> inventoryRadioIds = new Dictionary<Player, Dictionary<int, uint>>();
        private readonly Dictionary<Player, uint> droppingDict = new Dictionary<Player, uint>();
        private uint newRadioId = 0;

        private void Server_RestartingRound()
        {
            this.playerRadioDictionary.Clear();
            this.droppingDict.Clear();
            this.newRadioId = 0;
            this.disabledFloorRadios.Clear();
            this.radioIds.Clear();
            this.inventoryRadioIds.Clear();
            this.radioOwners.Clear();
        }

        private Radio GetRadio(Player player)
        {
            if (this.playerRadioDictionary.TryGetValue(player, out var radio))
                return radio;
            this.playerRadioDictionary[player] = player.GameObject.GetComponent<Radio>();
            return this.playerRadioDictionary[player];
        }

        private void Player_Dying(Exiled.Events.EventArgs.DyingEventArgs ev)
        {
            if (!ev.IsAllowed)
                return;
            if (ev.Target.HasItem(ItemType.Radio))
            {
                ev.Target.SetGUI("radio", PseudoGUIPosition.MIDDLE, null);
                if (!this.inventoryRadioIds.ContainsKey(ev.Target))
                    this.inventoryRadioIds[ev.Target] = new Dictionary<int, uint>();
                foreach (var item in ev.Target.Items.Where(x => x.id == ItemType.Radio).ToArray())
                {
                    if (!this.inventoryRadioIds[ev.Target].ContainsKey(item.uniq))
                    {
                        if (this.GetRadio(ev.Target).curPreset == 0)
                            this.disabledFloorRadios.Add(this.newRadioId);
                        this.radioIds[ev.Target.Inventory.SetPickup(ItemType.Radio, item.durability, ev.Target.Position, Quaternion.identity, 0, 0, 0, true)] = this.newRadioId++;
                        ev.Target.RemoveItem(item);
                        continue;
                    }

                    if (this.GetRadio(ev.Target).curPreset == 0)
                        this.disabledFloorRadios.Add(this.inventoryRadioIds[ev.Target][item.uniq]);
                    this.radioIds[ev.Target.Inventory.SetPickup(ItemType.Radio, item.durability, ev.Target.Position, Quaternion.identity, 0, 0, 0, true)] = this.inventoryRadioIds[ev.Target][item.uniq];
                    ev.Target.RemoveItem(item);
                    this.inventoryRadioIds[ev.Target].Clear();
                }
            }
        }

        private void Player_PickingUpItem(Exiled.Events.EventArgs.PickingUpItemEventArgs ev)
        {
            if (!ev.IsAllowed)
                return;
            if (ev.Pickup.ItemId != ItemType.Radio)
                return;
            if (!this.inventoryRadioIds.ContainsKey(ev.Player))
                this.inventoryRadioIds[ev.Player] = new Dictionary<int, uint>();
            var item = new Inventory.SyncItemInfo
            {
                id = ItemType.Radio,
                durability = ev.Pickup.durability,
            };
            ev.Player.AddItem(item);
            if (this.radioIds.ContainsKey(ev.Pickup))
            {
                this.inventoryRadioIds[ev.Player][Inventory._uniqId] = this.radioIds[ev.Pickup];
                this.radioIds.Remove(ev.Pickup);
            }
            else
                this.inventoryRadioIds[ev.Player][Inventory._uniqId] = this.newRadioId++;

            this.disabledFloorRadios.Remove(this.inventoryRadioIds[ev.Player][Inventory._uniqId]);
            ev.Pickup.Delete();
            ev.IsAllowed = false;
        }

        private void Player_ItemDropped(Exiled.Events.EventArgs.ItemDroppedEventArgs ev)
        {
            if (!this.droppingDict.ContainsKey(ev.Player))
                return;

            if (ev.Pickup.ItemId != ItemType.Radio)
            {
                this.droppingDict.Remove(ev.Player);
                return;
            }

            this.radioIds[ev.Pickup] = this.droppingDict[ev.Player];
            this.droppingDict.Remove(ev.Player);
        }

        private void Player_DroppingItem(Exiled.Events.EventArgs.DroppingItemEventArgs ev)
        {
            if (!ev.IsAllowed)
                return;
            if (ev.Item.id != ItemType.Radio)
                return;
            if (ev.Player.CurrentItem.uniq == ev.Item.uniq)
                ev.Player.SetGUI("radio", PseudoGUIPosition.MIDDLE, null);
            if (!this.inventoryRadioIds.ContainsKey(ev.Player))
                this.inventoryRadioIds[ev.Player] = new Dictionary<int, uint>();
            if (!this.inventoryRadioIds[ev.Player].ContainsKey(ev.Item.uniq))
                this.droppingDict[ev.Player] = this.newRadioId++;
            else
            {
                this.droppingDict[ev.Player] = this.inventoryRadioIds[ev.Player][ev.Item.uniq];
                this.inventoryRadioIds[ev.Player].Remove(ev.Item.uniq);
            }

            if (this.GetRadio(ev.Player).curPreset == 0)
                this.disabledFloorRadios.Add(this.droppingDict[ev.Player]);
            this.CallDelayed(1, () =>
            {
                if (!this.droppingDict.ContainsKey(ev.Player))
                    return;
                if (ev.Player.Items.Any(x => x.uniq == ev.Item.uniq))
                {
                    this.inventoryRadioIds[ev.Player][ev.Item.uniq] = this.droppingDict[ev.Player];
                    this.disabledFloorRadios.Remove(this.droppingDict[ev.Player]);
                }

                this.droppingDict.Remove(ev.Player);
            });
        }

        private void Player_Handcuffing(Exiled.Events.EventArgs.HandcuffingEventArgs ev)
        {
            if (!ev.IsAllowed)
                return;
            if (ev.Target.HasItem(ItemType.Radio))
            {
                ev.Target.SetGUI("radio", PseudoGUIPosition.MIDDLE, null);
                if (!this.inventoryRadioIds.ContainsKey(ev.Target))
                    this.inventoryRadioIds[ev.Target] = new Dictionary<int, uint>();
                foreach (var item in ev.Target.Items.ToArray().Where(x => x.id == ItemType.Radio))
                {
                    if (!this.inventoryRadioIds[ev.Target].ContainsKey(item.uniq))
                    {
                        if (this.GetRadio(ev.Target).curPreset == 0)
                            this.disabledFloorRadios.Add(this.newRadioId);
                        this.radioIds[ev.Target.Inventory.SetPickup(ItemType.Radio, item.durability, ev.Target.Position, Quaternion.identity, 0, 0, 0, true)] = this.newRadioId++;
                        ev.Target.RemoveItem(item);
                        continue;
                    }

                    if (this.GetRadio(ev.Target).curPreset == 0)
                        this.disabledFloorRadios.Add(this.inventoryRadioIds[ev.Target][item.uniq]);
                    this.radioIds[ev.Target.Inventory.SetPickup(ItemType.Radio, item.durability, ev.Target.Position, Quaternion.identity, 0, 0, 0, true)] = this.inventoryRadioIds[ev.Target][item.uniq];
                    ev.Target.RemoveItem(item);
                }
            }
        }

        private void Player_ChangingRadioPreset(Exiled.Events.EventArgs.ChangingRadioPresetEventArgs ev)
        {
            if (!ev.IsAllowed)
                return;
            if (ev.Player.CurrentItem.id != ItemType.Radio)
                return;
            this.CallDelayed(0.1f, () =>
            {
                if (ev.NewValue == 0)
                    ev.Player.SetGUI("radio", PseudoGUIPosition.MIDDLE, null);
                else
                    ev.Player.SetGUI("radio", PseudoGUIPosition.MIDDLE, this.GetDisplay());
            });
        }

        private void Player_ChangingItem(Exiled.Events.EventArgs.ChangingItemEventArgs ev)
        {
            if (ev.NewItem.id == ItemType.Radio && ev.OldItem.id != ItemType.Radio)
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
            while (player.IsAlive && player.CurrentItem.id == ItemType.Radio);
            player.SetGUI("radio", PseudoGUIPosition.MIDDLE, null);
        }

        private void SingleUpdateGUI(Player player)
        {
            if (this.GetRadio(player).curPreset != 0 && player.CurrentItem.id == ItemType.Radio)
                player.SetGUI("radio", PseudoGUIPosition.MIDDLE, this.GetDisplay());
            else
                player.SetGUI("radio", PseudoGUIPosition.MIDDLE, null);
        }

        private void Player_ChangedRole(Exiled.Events.EventArgs.ChangedRoleEventArgs ev)
        {
            if (!ev.Player.Items.Any(x => x.id == ItemType.Radio))
                return;
            this.inventoryRadioIds[ev.Player] = new Dictionary<int, uint>();
            uint radioId = this.newRadioId++;
            this.inventoryRadioIds[ev.Player][ev.Player.Items.First(x => x.id == ItemType.Radio).uniq] = radioId;
            this.radioOwners[radioId] = $"[TMP] {ev.Player.GetDisplayName()}";
            void Action() => this.radioOwners[radioId] = $"[{ev.Player.UnitName}] {ev.Player.GetDisplayName()}";
            if (ev.OldRole == RoleType.Spectator)
                this.CallDelayed(0.1f, Action);
            else
                Action();
        }

        private string GetDisplay()
        {
            Dictionary<uint, string> toWrite = new Dictionary<uint, string>();
            List<string> active = new List<string>();
            List<string> notActive = new List<string>();
            foreach (var player in RealPlayers.List.Where(x => x.Items.Any(item => item.id == ItemType.Radio)))
            {
                var item = player.Inventory.items.First(x => x.id == ItemType.Radio);
                if (!this.inventoryRadioIds.ContainsKey(player))
                    this.inventoryRadioIds[player] = new Dictionary<int, uint>();
                if (!this.inventoryRadioIds[player].ContainsKey(item.uniq))
                    this.inventoryRadioIds[player][item.uniq] = this.newRadioId++;

                uint radioId = this.inventoryRadioIds[player][item.uniq];
                if (!this.radioOwners.ContainsKey(radioId))
                    this.radioOwners[radioId] = "[UnKnOWn] " + this.GetNoise(radioId);
                if (this.GetRadio(player).curPreset == 0)
                    toWrite[radioId] = $"<color=#d4d4d4>{this.radioOwners[radioId]}</color>";
                else
                    toWrite[radioId] = $"<color=green>{this.radioOwners[radioId]}</color>";
            }

            foreach (var item in this.radioIds)
            {
                if (!this.radioOwners.ContainsKey(item.Value))
                    this.radioOwners[item.Value] = "[UnKnOWn] " + this.GetNoise(item.Value);
                if (this.disabledFloorRadios.Contains(item.Value))
                    toWrite[item.Value] = $"<color=#d4d4d4>{this.radioOwners[item.Value]}</color>";
                else
                    toWrite[item.Value] = $"<color=red>{this.radioOwners[item.Value]}</color>";
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
