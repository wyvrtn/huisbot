﻿using Discord;
using Discord.Interactions;
using huisbot.Models.Huis;
using huisbot.Models.Osu;
using huisbot.Modules.Autocompletes;
using huisbot.Services;
using Microsoft.Extensions.DependencyInjection;

namespace huisbot.Modules.Huis;

/// <summary>
/// The interaction module for the player command, displaying info about a player in a rework.
/// </summary>
public class PlayerCommandModule : InteractionModuleBase<SocketInteractionContext>
{
  private readonly OsuApiService _osu;
  private readonly HuisApiService _huis;

  public PlayerCommandModule(OsuApiService osu, HuisApiService huis)
  {
    _osu = osu;
    _huis = huis;
  }

  [SlashCommand("player", "Displays info about the specified player in the specified rework.")]
  public async Task HandleAsync(
    [Summary("player", "The osu! ID or name of the player.")] string playerId,
    [Summary("rework", "An identifier for the rework. This can be it's ID, internal code or autocompleted name.")]
    [Autocomplete(typeof(ReworkAutocompleteHandler))] string reworkId)
  {
    await DeferAsync();

    // Get all reworks and check whether the request was successful. If not, notify the user about an internal error.
    HuisRework[]? reworks = await _huis.GetReworksAsync();
    if (reworks is null)
    {
      await FollowupAsync(embed: Embeds.InternalError("Failed to get the reworks from the Huis API."));
      return;
    }

    // Try to get the specified rework by the specified identifier. If it doesn't exist, notify the user.
    HuisRework? rework = reworks.FirstOrDefault(x => x.Id.ToString() == reworkId || x.Code == reworkId || x.Name == reworkId);
    if (rework is null)
    {
      await FollowupAsync(embed: Embeds.Error($"The rework `{reworkId}` could not be found."));
      return;
    }

    // Get the user from the osu! api. If it failed or the user could not be found, notify the user.
    OsuUser? user = await _osu.GetUserAsync(playerId);
    if (user is null)
    {
      await FollowupAsync(embed: Embeds.InternalError("Failed to resolve the user from the osu! API."));
      return;
    }
    else if (!user.WasFound)
    {
      await FollowupAsync(embed: Embeds.Error($"No player with identifier `{playerId}` could not be found."));
      return;
    }

    // Get the player from the specified rework and check whether the request was successful. If not, notify the user about an internal error.
    HuisPlayer? player = await _huis.GetPlayerAsync(user.Id, rework.Id);
    if (player is null)
    {
      await FollowupAsync(embed: Embeds.InternalError("Failed to get the player from the Huis API."));
      return;
    }
    // If the player was successfully received but is uncalculated, queue the player if necessary and notify the user.
    else if (!player.IsCalculated)
    {
      // Get the queue and check whether the request was successful. If not, notify the user about an internal error.
      HuisQueue? queue = await _huis.GetQueueAsync();
      if (queue is null)
      {
        await FollowupAsync(embed: Embeds.InternalError("Failed to get the player calculation queue from the Huis API."));
        return;
      }

      // Check whether the player is already queued. If so, notify the user.
      if (queue.Entries!.Any(x => x.UserId == user.Id && x.ReworkId == rework.Id))
      {
        await FollowupAsync(embed: Embeds.Neutral($"The player `{user.Name}` is currently being calculated. Please try again later."));
        return;
      }

      // Queue the player and notify the user whether it was successful.
      bool queued = await _huis.QueuePlayerAsync(user.Id, rework.Id);
      if (queued)
        await FollowupAsync(embed: Embeds.Success($"The player `{user.Name}` has been added to the calculation queue."));
      else
        await FollowupAsync(embed: Embeds.InternalError($"Failed to queue the player `{user.Name}`."));

      return;
    }


    // Show the player embed.
    await FollowupAsync(embed: Embeds.Player(player, rework));
  }
}