﻿using huisbot.Models.Huis;
using huisbot.Models.Osu;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace huisbot.Services;

/// <summary>
/// The osu! API service is responsible for communicating with the osu! v1 API @ https://pp-api.huismetbenen.nl/.
/// </summary>
public class OsuApiService
{
  private readonly HttpClient _http;
  private readonly IConfiguration _config;
  private readonly ILogger<OsuApiService> _logger;

  public OsuApiService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<OsuApiService> logger)
  {
    _http = httpClientFactory.CreateClient("osuapi");
    _config = config;
    _logger = logger;
  }

  /// <summary>
  /// Returns a bool whether a connection to the osu! API can be established.
  /// </summary>
  /// <returns>Bool whether a connection can be established.</returns>
  public async Task<bool> IsAvailableAsync()
  {
    try
    {
      // Try to send a request to the base URL of the osu! API.
      HttpResponseMessage response = await _http.GetAsync("");

      // Check whether it returns the expected result.
      if (response.StatusCode != HttpStatusCode.Redirect)
        throw new Exception($"API returned status code {response.StatusCode}. Expected: Redirect (302).");

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError("IsAvailable() returned false: {Message}", ex.Message);
      return false;
    }
  }

  /// <summary>
  /// Returns the the osu! user by the specified ID or name. Returns <see cref="OsuUser.NotFound"/> if the user could not be found.
  /// </summary>
  /// <param name="identifier">The identifier the user.</param>
  /// <returns>The user.</returns>
  public async Task<OsuUser?> GetUserAsync(string identifier)
  {
    try
    {
      // Get the user from the API.
      string json = await _http.GetStringAsync($"get_user?u={identifier}&k={_config.GetValue<string>("OSU_API_KEY")}");
      OsuUser? user = JsonConvert.DeserializeObject<OsuUser[]>(json)?.FirstOrDefault();

      // Check whether the deserialized json is null/an empty array. If so, the user could not be found. The API returns "[]" when the user could not be found.
      if (user is null)
        return OsuUser.NotFound;

      return user;
    }
    catch (Exception ex)
    {
      _logger.LogError("Failed to get the user with identifier \"{Identifier}\" from the osu! API: {Message}", identifier, ex.Message);
      return null;
    }
  }

  /// <summary>
  /// Returns the beatmap set ID for the specified beatmap ID.
  /// </summary>
  /// <returns>The Beatmap Set ID of the specified beatmap.</returns>
  public async Task<OsuBeatmap?> GetBeatmapAsync(int id)
  {
    try
    {
      // Get the user from the API.
      string json = await _http.GetStringAsync($"get_beatmaps?b={id}&k={_config.GetValue<string>("OSU_API_KEY")}");
      OsuBeatmap? map = JsonConvert.DeserializeObject<OsuBeatmap[]>(json)?.FirstOrDefault(x => x.BeatmapId == id);

      // Check whether the deserialized json is null. If so, the user could not be found. The API returns "[]" when the user could not be found.
      if (map is null)
        throw new Exception("Deserialization of JSON returned null.");

      return map;
    }
    catch (Exception ex)
    {
      _logger.LogError("Failed to get the beatmap with ID {Id} from the osu! API: {Message}", id, ex.Message);
      return null;
    }
  }
}