﻿using Discord.Interactions;
using huisbot.Services;
using Microsoft.Extensions.Configuration;

namespace huisbot.Modules.Miscellaneous;

/// <summary>
/// The partial interaction module for the misc group & various subcommands, providing miscellaneous utility commands.
/// </summary>
[Group("misc", "Miscellaneous utility commands.")]
public partial class MiscellaneousCommandModule(IServiceProvider services, IConfiguration configuration) : ModuleBase(services, configuration);