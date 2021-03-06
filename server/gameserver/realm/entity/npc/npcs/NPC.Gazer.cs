﻿#region

using LoESoft.Core.config;
using LoESoft.Core.database;
using LoESoft.GameServer.realm.entity.player;
using System;
using System.Collections.Generic;
using System.Linq;
using static LoESoft.GameServer.realm.entity.player.Player;

#endregion

namespace LoESoft.GameServer.realm.entity.npc.npcs
{
    public class Gazer : NPC
    {
        public static IEnumerable<List<T>> SplitList<T>(List<T> locations, int nSize)
        {
            for (int i = 0; i < locations.Count; i += nSize)
                yield return locations.GetRange(i, Math.Min(nSize, locations.Count - i));
        }

        public override void Welcome(Player player) => Callback(player, $"Hello {player.Name}! I'm Gazer, how can I help you?");

        public override void Leave(Player player, bool polite) => Callback(player, polite ? _NPCLeaveMessage.Replace("{PLAYER}", player.Name) : "How rude!");

        public override void Commands(Player player, string command)
        {
            string callback = null;
            string task = null;
            var isTask = false;

            if (command.Length >= 6)
            {
                var taskStart = command;
                var taskOriginalNames = GameTask.Tasks.Keys.ToList();
                var taskToLowerNames = taskOriginalNames.Select(names => names.ToLower()).ToList();

                if (taskStart.Contains("start "))
                    taskStart = taskStart.Substring(6);

                task = taskToLowerNames.Contains(taskStart) ? taskOriginalNames.FirstOrDefault(name => name.ToLower() == taskStart) : null;
                isTask = task != null;
            }

            if (!isTask)
                switch (command)
                {
                    #region "Uptime"
                    case "uptime":
                        {
                            TimeSpan uptime = DateTime.Now - GameServer.Uptime;
                            double thisUptime = uptime.TotalMinutes;
                            if (thisUptime <= 1)
                                callback = "Server started recently.";
                            else if (thisUptime > 1 && thisUptime <= 59)
                                callback = string.Format("Uptime: {0}{1}{2}{3}.",
                                    $"{uptime.Minutes:n0}",
                                    (uptime.Minutes >= 1 && uptime.Minutes < 2) ? " minute" : " minutes",
                                    uptime.Seconds < 1 ? "" : $" and {uptime.Seconds:n0}",
                                    uptime.Seconds < 1 ? "" : (uptime.Seconds >= 1 && uptime.Seconds < 2) ? " second" : " seconds");
                            else
                                callback = string.Format("Uptime: {0}{1}{2}{3}{4}{5}.",
                                    $"{uptime.Hours:n0}",
                                    (uptime.Hours >= 1 && uptime.Hours < 2) ? " hour" : " hours",
                                    uptime.Minutes < 1 ? "" : $", {uptime.Minutes:n0}",
                                    uptime.Minutes < 1 ? "" : (uptime.Minutes >= 1 && uptime.Minutes < 2) ? " minute" : " minutes",
                                    uptime.Seconds < 1 ? "" : $" and {uptime.Seconds:n0}",
                                    uptime.Seconds < 1 ? "" : (uptime.Seconds >= 1 && uptime.Seconds < 2) ? " second" : " seconds");
                        }
                        break;
                    #endregion
                    #region "Online"
                    case "online":
                        {
                            int serverMaxUsage = Settings.NETWORKING.MAX_CONNECTIONS;
                            int serverCurrentUsage = GameServer.Manager.ClientManager.Count;
                            int worldCurrentUsage = player.Owner.Players.Keys.Count;
                            callback = $"Server: {serverCurrentUsage}/{serverMaxUsage} player{(serverCurrentUsage > 1 ? "s" : "")} | {player.Owner.Name}: {worldCurrentUsage} player{(worldCurrentUsage > 1 ? "s" : "")}.";
                        }
                        break;
                    #endregion
                    #region "Tasks"
                    case "task":
                    case "tasks":
                        callback = "Yeah! There are several tasks to do in-game and you can also receive wonderful rewards! Do you want more 'tasks info'?";
                        break;

                    case "task info":
                    case "tasks info":
                        callback = "The tasks are based in star requirement and some could be repeated. Tell me when you are 'ready' to begin any of them.";
                        break;

                    case "ready":
                        var gameTasks = SplitList(GameTask.Tasks.Keys.ToList(), 5).ToList();
                        var gameTaskNames = new List<string>();

                        foreach (var i in gameTasks)
                            gameTaskNames.Add(Utils.ToCommaSepString(i.ToArray()));

                        player.SendHelp("Tasks:");

                        foreach (var j in gameTaskNames)
                            player.SendHelp($"- {j}");

                        callback = "So far I have the following tasks, check your chat box and say the name of task to me to start. Remember, you can only handle one task per time until completed.";
                        break;

                    case "task status":
                        if (player.ActualTask == null)
                            callback = "You are in none task at this moment.";
                        else
                        {
                            var getTaskMonsters = player.MonsterCaches.Where(monster => monster.TaskLimit != -1);
                            var taskData = new List<string>();

                            foreach (var i in GameTask.Tasks[player.ActualTask].MonsterDatas)
                            {
                                var mob = getTaskMonsters.FirstOrDefault(monster => monster.ObjectId == i.ObjectId);

                                if (mob == null)
                                    taskData.Add($"- [0/{i.Total}] Defeat {i.Total} x {i.ObjectId}.");
                                else
                                    taskData.Add($"- [{mob.TaskCount}/{i.Total}] Defeat {i.Total} x {mob.ObjectId}{(mob.TaskCount >= i.Total ? " (OK!)" : "")}.");
                            }

                            player.SendHelp($"[Task] {player.ActualTask}:");

                            foreach (var j in taskData)
                                player.SendHelp(j);

                            callback = $"You are doing the '{player.ActualTask}' task and your status are displaying at chat box. You can ask for 'task reward' when you finish this task.";
                        }
                        break;

                    case "task reward":
                        if (player.ActualTask == null)
                            callback = "There is no reward for people who didn't finish any task with me. Come back here later...";
                        else
                        {
                            var currentTask = GameTask.Tasks[player.ActualTask];
                            var getTaskMonsters = player.MonsterCaches.Where(monster => monster.TaskLimit != -1);
                            var success = 0;

                            foreach (var i in currentTask.MonsterDatas)
                            {
                                var mob = getTaskMonsters.FirstOrDefault(monster => monster.ObjectId == i.ObjectId);

                                if (mob == null)
                                    continue;

                                if (mob.TaskCount >= i.Total)
                                    success++;
                            }

                            if (success == currentTask.MonsterDatas.Count)
                            {
                                foreach (var i in currentTask.MonsterDatas)
                                {
                                    var mob = getTaskMonsters.FirstOrDefault(monster => monster.ObjectId == i.ObjectId);

                                    if (mob == null)
                                        continue;

                                    mob.TaskCount = mob.TaskLimit = -1;
                                }

                                var gifts = player.Client.Account.Gifts.ToList();

                                for (var i = 0; i < currentTask.RewardDatas.Count; i++)
                                    for (var j = 0; j < currentTask.RewardDatas[i].Total; j++)
                                        gifts.Add(GameServer.Manager.GameData.IdToObjectType[currentTask.RewardDatas[i].ObjectId]);

                                player.Client.Account.Gifts = gifts.ToArray();
                                player.Client.Account.Flush();
                                player.Client.Account.Reload();

                                currentTask.Bonus?.Invoke(player);
                                currentTask.GetAchievement(player.ActualTask, player);

                                callback = $"Congratulations {player.Name}! You have finished the task '{player.ActualTask}'.";
                            }
                            else
                                callback = "You didn't finish your task properly, ask me for 'task status' for more details.";
                        }
                        break;
                    #endregion
                    #region "Help"
                    case "help":
                        callback = "You can ask me about 'uptime', 'online' and 'tasks' for more details.";
                        break;
                    #endregion
                    case "hi":
                    case "hello":
                    case "hey":
                    case "good morning":
                    case "good afternoon":
                    case "good evening":
                        Callback(player, command, false); // player only (self)
                        NoRepeat(player);
                        return;

                    case "bye":
                    case "goodbye":
                    case "goodnight":
                        RemovePlayer(player);
                        Callback(player, command, false); // player only (self)
                        Leave(player, true);
                        return;

                    default:
                        callback = "Sorry, I don't understand. Say 'help' for more details.";
                        break;
                }
            else
            {
                if (!command.ToLower().Contains("start ")) // task details
                {
                    if (player.Stars >= GameTask.Tasks[task].StarsRequirement)
                        callback = $"You seems prepared to initialize this task, just say 'start {task}' to begin!";
                    else
                        callback = $"You are too young at this moment to do '{task}' task. Come back here later...";
                }
                else // task start
                {
                    var getTask = GameTask.Tasks[task];

                    if (player.ActualTask == null)
                    {
                        if (player.Achievements.Contains(task) && !getTask.MultipleTimes)
                            callback = $"You cannot do '{task}' task again.";
                        else
                        {
                            if (player.Stars >= getTask.StarsRequirement)
                            {
                                var monsterCaches = new List<MonsterCache>();

                                foreach (var i in getTask.MonsterDatas)
                                    monsterCaches.Add(new MonsterCache()
                                    {
                                        ObjectId = i.ObjectId,
                                        TaskCount = 0,
                                        TaskLimit = i.Total,
                                        Total = 0
                                    });

                                foreach (var j in monsterCaches)
                                {
                                    var mob = player.MonsterCaches.FirstOrDefault(monster => monster.ObjectId == j.ObjectId);

                                    if (mob == null)
                                        player.MonsterCaches.Add(j);
                                    else
                                    {
                                        mob.TaskCount = j.TaskCount;
                                        mob.TaskLimit = j.TaskLimit;
                                    }
                                }

                                player.ActualTask = task;
                                player.Task = getTask;
                                player.UpdateCount++;
                                player.SaveToCharacter();

                                callback = $"You have successfully initialized '{task}' task and all I want to say is good luck! Come back here and ask me for 'task reward' when you finish your task!";
                            }
                            else
                                callback = $"You are too young at this moment to do '{task}' task. Come back here later...";
                        }
                    }
                    else
                    {
                        if (player.ActualTask != null)
                            callback = $"You are already in '{player.ActualTask}' task and cannot begin another one until you finish yours. Also, you can ask me for 'task status' to show your progress.";
                        else
                            callback = $"I could give you permission to start this '{task}' task, but I'm gonna?";
                    }
                }
            }
            Callback(player, command, false); // player only (self)
            Callback(player, callback); // to NPC
            ChatManager.ChatDataCache.Remove(player.Name); // Removing player from chat data cache.
        }
    }
}