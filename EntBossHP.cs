using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;

namespace EntBossHP
{
    public class EntBossHP : BasePlugin
    {
        public override string ModuleName => "EntBossHP";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "Oylsister, Kxrnl";

        public Dictionary<CCSPlayerController, float> ClientLastShootHitBox = new Dictionary<CCSPlayerController, float>();
        public Dictionary<CCSPlayerController, CEntityInstance> ClientEntityHit = new Dictionary<CCSPlayerController, CEntityInstance>();
        public Dictionary<CCSPlayerController, string> ClientEntityNameHit = new Dictionary<CCSPlayerController, string>();
        public float CurrentTime;
        public float LastForceShowBossHP;

        public override void Load(bool hotReload)
        {
            HookEntityOutput("math_counter", "OutValue", CounterOut);
            HookEntityOutput("func_physbox_multiplayer", "OnDamaged", BreakableOut);
            HookEntityOutput("func_physbox", "OnHealthChanged", BreakableOut);
            HookEntityOutput("func_breakable", "OnHealthChanged", BreakableOut);
            HookEntityOutput("prop_dynamic", "OnHealthChanged", Hitbox_Hook);

            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
        }

        public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            if (@event.Userid.IsBot || @event.Userid.IsHLTV)
                return HookResult.Continue;

            ClientLastShootHitBox.Add(@event.Userid, 0.0f);
            ClientEntityHit.Add(@event.Userid, null);
            ClientEntityNameHit.Add(@event.Userid, null);

            return HookResult.Continue;
        }

        public void OnClientDisconnect(int playerslot)
        {
            var client = Utilities.GetPlayerFromSlot(playerslot);

            if (client.IsBot || client.IsHLTV)
                return;

            ClientLastShootHitBox.Remove(client);
            ClientEntityHit.Remove(client);
            ClientEntityNameHit.Remove(client);
        }

        private unsafe float GetMathCounterValue(nint handle)
        {
            var offset = Schema.GetSchemaOffset("CMathCounter", "m_OutValue");
            return *(float*)IntPtr.Add(handle, offset + 24);
        }

        public HookResult CounterOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (!caller.IsValid)
                return HookResult.Continue;

            if (caller.DesignerName != "math_counter")
                return HookResult.Continue;

            var entityname = caller.Entity.Name;
            var hp = GetMathCounterValue(caller.Handle);

            if (player(activator) == null)
                return HookResult.Continue;

            if (ClientLastShootHitBox[player(activator)] > Server.EngineTime - 0.2f)
            {
                ClientEntityHit[player(activator)] = caller;
                ClientEntityNameHit[player(activator)] = caller.Entity.Name;

                if (hp > 0)
                {
                    Print_BHUD(player(activator), caller, entityname, (int)Math.Round(hp));
                }
            }

            return HookResult.Continue;
        }

        public HookResult BreakableOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {

            if (!activator.IsValid || !ClientLastShootHitBox.ContainsKey(player(activator)))
                return HookResult.Continue;

            ClientLastShootHitBox[player(activator)] = Server.EngineTime;

            return HookResult.Continue;
        }

        public HookResult Hitbox_Hook(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (!activator.IsValid || !ClientLastShootHitBox.ContainsKey(player(activator)))
                return HookResult.Continue;

            if (!caller.IsValid)
                return HookResult.Continue;

            var entityname = caller.Entity.Name;
            CBreakable prop = new CBreakable(caller.Handle);

            if (!prop.IsValid || prop == null)
                return HookResult.Continue;

            var hp = prop!.Health;

            if (hp > 500000)
                return HookResult.Continue;

            if (player(activator) == null)
                return HookResult.Continue;

            if (ClientLastShootHitBox[player(activator)] > Server.EngineTime - 0.2f)
            {
                if (string.IsNullOrEmpty(entityname))
                    entityname = "HP";

                ClientEntityHit[player(activator)] = caller;
                ClientEntityNameHit[player(activator)] = caller.Entity.Name;

                if (hp <= 0)
                {
                    Print_BHUD(player(activator), caller, entityname, 0);
                }

                else
                {
                    Print_BHUD(player(activator), caller, entityname, hp);
                }
            }

            ClientLastShootHitBox[player(activator)] = Server.EngineTime;

            return HookResult.Continue;
        }

        void Print_BHUD(CCSPlayerController client, CEntityInstance entity, string name, int hp)
        {
            CurrentTime = Server.EngineTime;

            if (ClientLastShootHitBox[client] > CurrentTime - 3.0f && LastForceShowBossHP + 0.1f < CurrentTime || hp == 0)
            {
                var playercount = 0;
                var CTcount = 0;

                foreach (var player in Utilities.GetPlayers())
                {
                    if (player.Team == CsTeam.CounterTerrorist)
                    {
                        CTcount++;
                        if (ClientLastShootHitBox[player] > CurrentTime - 7.0 && ClientEntityHit[player] == entity && name == ClientEntityNameHit[player])
                        {
                            playercount++;
                        }
                    }
                }

                if (playercount > CTcount / 2)
                {
                    foreach (var player in Utilities.GetPlayers())
                    {
                        player.PrintToCenter($"{name}: {hp}");
                    }
                }
                else
                {
                    foreach (var player in Utilities.GetPlayers())
                    {
                        if (ClientLastShootHitBox[player] > CurrentTime - 7.0f && ClientEntityHit[player] == entity && name == ClientEntityNameHit[player])
                        {
                            player.PrintToCenter($"{name}: {hp}");
                        }
                    }
                }

                LastForceShowBossHP = CurrentTime;
            }
        }

        public static CCSPlayerController player(CEntityInstance instance)
        {
            if (instance == null)
            {
                return null;
            }

            if (instance.DesignerName != "player")
            {
                return null;
            }

            // grab the pawn index
            int player_index = (int)instance.Index;

            // grab player controller from pawn
            CCSPlayerPawn player_pawn = Utilities.GetEntityFromIndex<CCSPlayerPawn>(player_index);

            // pawn valid
            if (player_pawn == null || !player_pawn.IsValid)
            {
                return null;
            }

            // controller valid
            if (player_pawn.OriginalController == null || !player_pawn.OriginalController.IsValid)
            {
                return null;
            }

            // any further validity is up to the caller
            return player_pawn.OriginalController.Value;
        }
    }
}
