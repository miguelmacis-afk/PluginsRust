using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MiVoteNight", "☠ Ⓜ🅘ⓖ🅤ⓔ🅛 ☠", "1.3.0")]
    public class MiVoteNight : RustPlugin
    {
        private HashSet<ulong> votes = new HashSet<ulong>();
        private bool canVoteThisNight = true;
        private bool isNightSkipped = false;
        private bool announceDone = false; 
        private const float VoteThreshold = 0.60f;

        #region Localization

        protected override void LoadDefaultMessages()
        {
            // INGLÉS
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NightStarted"] = "<color=#ffdb58>☀ Night has begun.</color> You can vote to skip it using <color=#5fb1ff>/voteday</color>.",
                ["NightCooldown"] = "<color=#ee6b6b>☀ Night Cooldown:</color> This night cannot be skipped. Take the chance to farm!",
                ["AdminNoVote"] = "Admins do not count in the vote.",
                ["OnlyAtNight"] = "You can only vote at night.",
                ["MandatoryNight"] = "This night is mandatory (cooldown).",
                ["AlreadyVoted"] = "You have already voted.",
                ["VoteRegistered"] = "<color=#5fb1ff>Vote registered:</color> {0}/{1} to skip the night.",
                ["SkipSuccess"] = "<color=#ffdb58>Goal reached! Skipping to dawn...</color>"
            }, this, "en");
			
			// ESPAÑOL
            var esMessages = new Dictionary<string, string>
			{
				["NightStarted"] = "<color=#ffdb58>☀ La noche ha comenzado.</color> Puedes votar para saltarla usando <color=#5fb1ff>/voteday</color>.",
				["NightCooldown"] = "<color=#ee6b6b>☀ Noche de Cooldown:</color> Esta noche no se puede saltar. ¡Aprovecha para farmear!",
				["AdminNoVote"] = "Los admins no cuentan en la votación.",
				["OnlyAtNight"] = "Solo puedes votar de noche.",
				["MandatoryNight"] = "Esta noche es obligatoria (cooldown).",
				["AlreadyVoted"] = "Ya has votado.",
				["VoteRegistered"] = "<color=#5fb1ff>Voto registrado:</color> {0}/{1} para saltar la noche.",
				["SkipSuccess"] = "<color=#ffdb58>¡Meta alcanzada! Saltando al amanecer...</color>"
			};

			lang.RegisterMessages(esMessages, this, "es");
			lang.RegisterMessages(esMessages, this, "es-ES");

            // RUSO
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NightStarted"] = "<color=#ffdb58>☀ Началась ночь.</color> Вы можете проголосовать за ее пропуск, используя <color=#5fb1ff>/voteday</color>.",
                ["NightCooldown"] = "<color=#ee6b6b>☀ Ночной кулдаун:</color> Эту ночь нельзя пропустить. Используйте это время для фарма!",
                ["AdminNoVote"] = "Админы не учитываются в голосовании.",
                ["OnlyAtNight"] = "Голосовать можно только ночью.",
                ["MandatoryNight"] = "Эта ночь обязательна (кулдаун).",
                ["AlreadyVoted"] = "Вы уже проголосовали.",
                ["VoteRegistered"] = "<color=#5fb1ff>Голос зарегистрирован:</color> {0}/{1} для пропуска ночи.",
                ["SkipSuccess"] = "<color=#ffdb58>Цель достигнута! Пропускаем до рассвета...</color>"
            }, this, "ru");

            // ITALIANO
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NightStarted"] = "<color=#ffdb58>☀ La notte è iniziata.</color> Puoi votare per saltarla usando <color=#5fb1ff>/voteday</color>.",
                ["NightCooldown"] = "<color=#ee6b6b>☀ Notte di Cooldown:</color> Questa notte non può essere saltata. Approfittane per farmare!",
                ["AdminNoVote"] = "Gli admin non contano nella votazione.",
                ["OnlyAtNight"] = "Puoi votare solo di notte.",
                ["MandatoryNight"] = "Questa notte è obbligatoria (cooldown).",
                ["AlreadyVoted"] = "Hai già votato.",
                ["VoteRegistered"] = "<color=#5fb1ff>Voto registrato:</color> {0}/{1} per saltare la notte.",
                ["SkipSuccess"] = "<color=#ffdb58>Obiettivo raggiunto! Saltando all'alba...</color>"
            }, this, "it");
        }

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion

        void OnServerInitialized() => timer.Repeat(10f, 0, CheckTime);

        private void CheckTime()
        {
            var hour = TOD_Sky.Instance.Cycle.Hour;
            bool isNight = hour >= 18 || hour < 8;

            if (!isNight)
            {
                if (announceDone)
                {
                    if (isNightSkipped) { canVoteThisNight = false; isNightSkipped = false; }
                    else { canVoteThisNight = true; }
                    
                    votes.Clear();
                    announceDone = false;
                }
            }
            else
            {
                if (!announceDone)
                {
                    AnnounceNightStatus();
                    announceDone = true;
                }
            }
        }

        private void AnnounceNightStatus()
        {
            string key = canVoteThisNight ? "NightStarted" : "NightCooldown";
            foreach (var player in BasePlayer.activePlayerList)
            {
                PrintToChat(player, GetMsg(key, player.UserIDString));
            }
        }

        [ChatCommand("voteday")]
        void CmdVoteDay(BasePlayer player)
        {
            if (player.IsAdmin) { 
                PrintToChat(player, GetMsg("AdminNoVote", player.UserIDString)); 
                return; 
            }

            var hour = TOD_Sky.Instance.Cycle.Hour;
            if (hour < 18 && hour > 8)
            {
                PrintToChat(player, GetMsg("OnlyAtNight", player.UserIDString));
                return;
            }

            if (!canVoteThisNight)
            {
                PrintToChat(player, GetMsg("MandatoryNight", player.UserIDString));
                return;
            }

            if (votes.Contains(player.userID))
            {
                PrintToChat(player, GetMsg("AlreadyVoted", player.UserIDString));
                return;
            }

            votes.Add(player.userID);

            int activePlayers = BasePlayer.activePlayerList.Count(p => !p.IsAdmin);
            int votesNeeded = Mathf.Max(1, Mathf.CeilToInt(activePlayers * VoteThreshold));
            
			foreach (var p in BasePlayer.activePlayerList)
			{
				PrintToChat(p, string.Format(GetMsg("VoteRegistered", p.UserIDString), votes.Count, votesNeeded));
			}

            if (votes.Count >= votesNeeded) SkipNight();
        }

        private void SkipNight()
        {
            TOD_Sky.Instance.Cycle.Hour = 8;
            isNightSkipped = true;
            votes.Clear();

            foreach (var player in BasePlayer.activePlayerList)
            {
                PrintToChat(player, GetMsg("SkipSuccess", player.UserIDString));
            }
        }
    }
}