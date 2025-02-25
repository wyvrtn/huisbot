﻿using Discord;
using Discord.Interactions;
using huisbot.Helpers;
using huisbot.Models.Huis;
using huisbot.Models.Osu;
using huisbot.Utilities;
using Microsoft.Extensions.Configuration;

namespace huisbot.Modules.Huis;

/// <summary>
/// The interaction module for the simulate command, calculating the score of a player in a rework.
/// </summary>
[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
[CommandContextType(InteractionContextType.BotDm, InteractionContextType.PrivateChannel, InteractionContextType.Guild)]
public class SimulateCommandModule(IServiceProvider services, IConfiguration configuration) : ModuleBase(services, configuration)
{
  [SlashCommand("simulate", "Simulates a score in the specified rework with the specified parameters.")]
  public async Task HandleAsync(
    [Summary("rework", "An identifier for the rework. This can be it's ID, internal code or autocompleted name.")]
    [Autocomplete(typeof(ReworkAutocompleteHandler))] string reworkId = "master",
    [Summary("referenceRework", "The reference rework to compare the score to. Defaults to the live PP system.")]
    [Autocomplete(typeof(ReworkAutocompleteHandler))] string? referenceReworkId = null,
    [Summary("beatmap", "The ID, URL or alias of the beatmap.")] string? beatmapId = null,
    [Summary("combo", "The maximum combo in the score.")] int? combo = null,
    [Summary("100s", "The amount of 100s/oks in the score.")] int? count100 = null,
    [Summary("50s", "The amount of 50s/mehs in the score.")] int? count50 = null,
    [Summary("misses", "The amount of misses in the score.")] int? misses = null,
    [Summary("largeTickMisses", "(Lazer) The amount of large tick misses in the score.")] int? largeTickMisses = null,
    [Summary("sliderTailMisses", "(Lazer) The amount of misses in the score.")] int? sliderTailMisses = null,
    [Summary("mods", "The mods used in the score.")] string? modsStr = null,
    [Summary("clockRate", "The clock rate of the score. Automatically adds DT/HT.")][MinValue(0.5)][MaxValue(2)] double clockRate = 1)
  {
    await DeferAsync();

    // Check if either a beatmap ID was specified, or if a recent bot message with a beatmap URL can be found.
    if (beatmapId is null)
    {
      // Look for a message with a score in the channel.
      if (await Utils.FindOsuBotScore(Context) is EmbedScoreInfo score)
      {
        beatmapId = score.BeatmapId.ToString();
        combo ??= score.Combo;
        count100 ??= score.Statistics?.Count100;
        count50 ??= score.Statistics?.Count50;
        misses ??= score.Statistics?.Misses;
        modsStr ??= score.Mods;
      }

      // If there was no beatmap ID found, respond with an error.
      if (beatmapId is null)
      {
        await FollowupAsync(embed: Embeds.Error("Please specify a beatmap."));
        return;
      }
    }

    // Get the matching rework for the specified rework identifier.
    HuisRework? rework = await GetReworkAsync(reworkId);
    if (rework is null)
      return;

    // Get the matching reference rework for the specified rework identifier.
    HuisRework? refRework = await GetReworkAsync(referenceReworkId ?? HuisRework.LiveId.ToString());
    if (refRework is null)
      return;

    // Parse the mod-related parameters.
    OsuMods mods = OsuMods.FromString(modsStr ?? "");
    mods.SetClockRate(clockRate);

    // Get the beatmap from the identifier.
    OsuBeatmap? beatmap = await GetBeatmapAsync(beatmapId!);
    if (beatmap is null)
      return;

    // Display the calculation progress in an embed to the user.
    IUserMessage msg = await FollowupAsync(embed: Embeds.Calculating(rework, rework == refRework ? null : refRework, false));

    // Get the local result from the Huis API and check whether it was successful.
    int? sliderTailHits = sliderTailMisses is null ? null : beatmap.SliderCount - sliderTailMisses.Value;
    OsuScoreStatistics statistics = new(count100, count50, misses, largeTickMisses, sliderTailHits);
    HuisCalculationResponse? localScore = await CalculateScoreAsync(new(beatmap, rework, mods, combo, statistics));
    if (localScore is null)
      return;

    // If the requested rework is the same as the reference, calculation is done here.
    HuisCalculationResponse? refScore = localScore;
    if (rework != refRework)
    {
      // Update the calculation progress embed.
      await ModifyOriginalResponseAsync(x => x.Embed = Embeds.Calculating(rework, refRework, true));

      // Get the reference rework result from the Huis API and check whether it was successful.
      refScore = await CalculateScoreAsync(new(beatmap, refRework, mods, combo, statistics));
      if (refScore is null)
        return;
    }

    // Send the result in an embed to the user.
    await ModifyOriginalResponseAsync(x => x.Embed = Embeds.CalculatedScore(localScore, refScore, rework, refRework, beatmap));
  }
}
