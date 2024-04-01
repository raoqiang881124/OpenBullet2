using System.Collections.Frozen;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using OpenBullet2.Core.Repositories;
using OpenBullet2.Core.Services;
using OpenBullet2.Web.Attributes;
using OpenBullet2.Web.Dtos.Common;
using OpenBullet2.Web.Dtos.Config;
using OpenBullet2.Web.Dtos.Config.Blocks;
using OpenBullet2.Web.Dtos.Config.Convert;
using OpenBullet2.Web.Dtos.ConfigDebugger;
using OpenBullet2.Web.Exceptions;
using OpenBullet2.Web.Services;
using OpenBullet2.Web.Utils;
using RuriLib.Extensions;
using RuriLib.Helpers;
using RuriLib.Helpers.Blocks;
using RuriLib.Helpers.Transpilers;
using RuriLib.Logging;
using RuriLib.Models.Blocks;
using RuriLib.Models.Configs;
using RuriLib.Models.Debugger;
using RuriLib.Models.Trees;
using RuriLib.Services;

namespace OpenBullet2.Web.Controllers;

/// <summary>
/// Manage configs.
/// </summary>
[Guest]
[ApiVersion("1.0")]
public class ConfigController : ApiController
{
    private readonly IConfigRepository _configRepo;
    private readonly ConfigService _configService;
    private readonly ILogger<ConfigController> _logger;
    private readonly IMapper _mapper;
    private readonly OpenBulletSettingsService _obSettingsService;
    private readonly LoliCodeAutocompletionService _loliCodeAutocompletionService;
    private readonly ConfigDebuggerService _configDebuggerService;
    private readonly PluginRepository _pluginRepository;

    /// <summary></summary>
    public ConfigController(IConfigRepository configRepo,
        PluginRepository pluginRepository,
        ConfigService configService, IMapper mapper,
        OpenBulletSettingsService obSettingsService,
        LoliCodeAutocompletionService loliCodeAutocompletionService,
        ConfigDebuggerService configDebuggerService,
        ILogger<ConfigController> logger)
    {
        _configRepo = configRepo;
        _pluginRepository = pluginRepository;
        _configService = configService;
        _mapper = mapper;
        _obSettingsService = obSettingsService;
        _loliCodeAutocompletionService = loliCodeAutocompletionService;
        _configDebuggerService = configDebuggerService;
        _logger = logger;
    }

    /// <summary>
    /// List all the available configs. Optionally, reload them from disk.
    /// </summary>
    [HttpGet("all")]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<IEnumerable<ConfigInfoDto>>> GetAll(
        bool reload = false)
    {
        if (reload)
        {
            await _configService.ReloadConfigsAsync();
        }

        var configs = _configService.Configs
            .OrderByDescending(c => c.Metadata.LastModified);

        var dtos = new List<ConfigInfoDto>();

        foreach (var config in configs)
        {
            var dto = _mapper.Map<ConfigInfoDto>(config);
            dto.Dangerous = config.HasCSharpCode();
            dtos.Add(dto);
        }

        return dtos;
    }

    /// <summary>
    /// Get a config's metadata.
    /// </summary>
    [HttpGet("metadata")]
    [MapToApiVersion("1.0")]
    public ActionResult<ConfigMetadataDto> GetMetadata(string id)
    {
        var config = GetConfigFromService(id);
        return _mapper.Map<ConfigMetadataDto>(config.Metadata);
    }

    /// <summary>
    /// Get a config's info.
    /// </summary>
    [HttpGet("info")]
    [MapToApiVersion("1.0")]
    public ActionResult<ConfigInfoDto> GetInfo(string id)
    {
        var config = GetConfigFromService(id);
        var dto = _mapper.Map<ConfigInfoDto>(config);
        dto.Dangerous = config.HasCSharpCode();
        return dto;
    }

    /// <summary>
    /// Get the readme of a config.
    /// </summary>
    [HttpGet("readme")]
    [MapToApiVersion("1.0")]
    public ActionResult<ConfigReadmeDto> GetReadme(string id)
    {
        var config = GetConfigFromService(id);

        return new ConfigReadmeDto { MarkdownText = config.Readme };
    }

    /// <summary>
    /// Get a config's data.
    /// </summary>
    [Admin]
    [HttpGet]
    [MapToApiVersion("1.0")]
    public ActionResult<ConfigDto> GetConfig(string id)
    {
        var config = GetConfigFromService(id);
        return _mapper.Map<ConfigDto>(config);
    }

    /// <summary>
    /// Update a config's data.
    /// </summary>
    [Admin]
    [HttpPut]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<ConfigDto>> UpdateConfig(UpdateConfigDto dto)
    {
        // Make sure a config with this id exists
        var config = GetConfigFromService(dto.Id);

        // Make sure it's not a remote config
        if (config.IsRemote)
        {
            _logger.LogWarning(
                "Attempted to edit a remote config with id {Id}", dto.Id);

            throw new ActionNotAllowedException(
                ErrorCode.ActionNotAllowedForRemoteConfig,
                $"Attempted to edit a remote config with id {dto.Id}");
        }

        // Check if we have all required plugins, and warn
        // the caller if we do not
        var loadedPlugins = _pluginRepository.GetPlugins();

        if (config.Metadata.Plugins is not null)
        {
            var missingPlugins = config.Metadata.Plugins.Where(
                p => !loadedPlugins.Any(lp => lp.FullName == p)).ToList();

            if (missingPlugins.Any())
            {
                SetWarning($"Missing plugins: [{string.Join(", ", missingPlugins)}]");
            }
        }

        // Apply the new fields to the existing config
        _mapper.Map(dto, config);

        // If the mode is Stack or LoliCode we also have to make sure to
        // update the Stack according to the new LoliCode
        if (config.Mode is ConfigMode.Stack or ConfigMode.LoliCode)
        {
            var stack = Loli2StackTranspiler.Transpile(config.LoliCodeScript);
            config.Stack = stack;
        }

        if (dto.Persistent)
        {
            // Save it
            await _configRepo.SaveAsync(config);

            _logger.LogInformation("Edited config with id {Id}", dto.Id);
        }

        return _mapper.Map<ConfigDto>(config);
    }

    /// <summary>
    /// Create a new config.
    /// </summary>
    [Admin]
    [HttpPost]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<ConfigDto>> CreateConfig()
    {
        var config = await _configRepo.CreateAsync();
        config.Metadata.Author = _obSettingsService.Settings.GeneralSettings.DefaultAuthor;
        await _configRepo.SaveAsync(config);
        _configService.Configs.Add(config);

        _logger.LogInformation("Created config with id {Id}", config.Id);

        return _mapper.Map<ConfigDto>(config);
    }

    /// <summary>
    /// Delete a config.
    /// </summary>
    [Admin]
    [HttpDelete]
    [MapToApiVersion("1.0")]
    public ActionResult DeleteConfig(string id)
    {
        var config = GetConfigFromService(id);
        _configRepo.Delete(config);
        _configService.Configs.Remove(config);

        _logger.LogInformation("Deleted config with id {Id}", id);

        return Ok();
    }

    /// <summary>
    /// Clone an existing config.
    /// </summary>
    [Admin]
    [HttpPost("clone")]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<ConfigDto>> CloneConfig(string id)
    {
        var original = GetConfigFromService(id);

        // Make sure it's not a remote config
        if (original.IsRemote)
        {
            _logger.LogWarning(
                "Attempted to edit a remote config with id {Id}", id);

            throw new ActionNotAllowedException(
                ErrorCode.ActionNotAllowedForRemoteConfig,
                $"Attempted to edit a remote config with id {id}");
        }

        // Pack and unpack to clone
        var packed = await ConfigPacker.PackAsync(original);
        using var ms = new MemoryStream(packed);
        var cloned = await ConfigPacker.UnpackAsync(ms);

        // Change the id and save it again
        cloned.Id = Guid.NewGuid().ToString();
        await _configRepo.SaveAsync(cloned);

        _configService.Configs.Add(cloned);

        _logger.LogInformation("Created config with id {ClonedId} by cloning {OriginalId}",
            cloned.Id, original.Id);

        return _mapper.Map<ConfigDto>(cloned);
    }

    /// <summary>
    /// Download a config as a .opk file.
    /// </summary>
    [Admin]
    [HttpGet("download")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> DownloadConfig(string id)
    {
        var config = GetConfigFromService(id);

        // Make sure it's not a remote config
        if (config.IsRemote)
        {
            _logger.LogWarning(
                "Attempted to download a remote config with id {Id}", id);

            throw new ActionNotAllowedException(
                ErrorCode.ActionNotAllowedForRemoteConfig,
                $"Attempted to download a remote config with id {id}");
        }

        var fileName = $"{config.Metadata.Name.ToValidFileName()}.opk";
        var bytes = await ConfigPacker.PackAsync(config);

        return File(bytes, "application/octet-stream", fileName);
    }

    /// <summary>
    /// Download all available configs as .opk files in a zip archive.
    /// </summary>
    [Admin]
    [HttpGet("download/all")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> DownloadAllConfigs()
    {
        // Do not download remote configs
        var configsToPack = _configService.Configs.Where(c => !c.IsRemote);
        var bytes = await ConfigPacker.PackAsync(configsToPack);

        return File(bytes, "application/zip", "configs.zip");
    }

    /// <summary>
    /// Upload configs as .opk archives. OB1 configs (.loli) are also
    /// accepted, and they will be automatically packed into a .opk archive.
    /// </summary>
    [Admin]
    [HttpPost("upload/many")]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<AffectedEntriesDto>> Upload(
        IFormFileCollection files)
    {
        foreach (var file in files)
        {
            await using var stream = file.OpenReadStream();
            await _configRepo.UploadAsync(stream, file.FileName);
        }

        // Reload from disk to get the new configs
        await _configService.ReloadConfigsAsync();

        _logger.LogInformation("Uploaded {FileCount} configs", files.Count);

        return new AffectedEntriesDto { Count = files.Count };
    }

    /// <summary>
    /// Convert a LoliCode script to C# script.
    /// </summary>
    [Admin]
    [HttpPost("convert/lolicode/csharp")]
    [MapToApiVersion("1.0")]
    public ActionResult<ConvertedCSharpDto> ConvertLoliCodeToCSharp(
        ConvertLoliCodeToCSharpDto dto)
    {
        var converted = Loli2CSharpTranspiler
            .Transpile(dto.LoliCode, _mapper.Map<ConfigSettings>(dto.Settings));

        return new ConvertedCSharpDto { CSharpScript = converted };
    }

    /// <summary>
    /// Convert a LoliCode script to a Stack of blocks.
    /// </summary>
    [Admin]
    [HttpPost("convert/lolicode/stack")]
    [MapToApiVersion("1.0")]
    public ActionResult<ConvertedStackDto> ConvertLoliCodeToStack(
        ConvertLoliCodeToStackDto dto)
    {
        var converted = Loli2StackTranspiler.Transpile(dto.LoliCode);

        // We cast to object to avoid the serializer from
        // serializing the wrong type (base class instead of derived)
        var stack = _mapper.Map<IEnumerable<BlockInstanceDto>>(converted)
            .Cast<object>().ToList();

        return new ConvertedStackDto { Stack = stack };
    }

    /// <summary>
    /// Convert a Stack of blocks to a LoliCode script.
    /// </summary>
    [Admin]
    [HttpPost("convert/stack/lolicode")]
    [MapToApiVersion("1.0")]
    public ActionResult<ConvertedLoliCodeDto> ConvertStackToLoliCode(
        ConvertStackToLoliCodeDto dto)
    {
        var mapped = dto.Stack.MapStack(_mapper);
        var converted = Stack2LoliTranspiler.Transpile(mapped);

        return new ConvertedLoliCodeDto { LoliCode = converted };
    }

    /// <summary>
    /// Get the descriptors of all the available blocks.
    /// </summary>
    /// <returns></returns>
    [Admin]
    [HttpGet("block-descriptors")]
    [MapToApiVersion("1.0")]
    public ActionResult<Dictionary<string, BlockDescriptorDto>> GetBlockDescriptors()
        => RuriLib.Globals.DescriptorsRepository.Descriptors.ToDictionary(
            kvp => kvp.Key,
            kvp => _mapper.Map<BlockDescriptorDto>(kvp.Value));

    private Config GetConfigFromService(string id)
    {
        var config = _configService.Configs.Find(c => c.Id == id);

        if (config is null)
        {
            throw new EntryNotFoundException(ErrorCode.ConfigNotFound,
                id, nameof(ConfigService));
        }

        return config;
    }

    /// <summary>
    /// Get the category tree of all the available blocks.
    /// </summary>
    [Admin]
    [HttpGet("category-tree")]
    [MapToApiVersion("1.0")]
    public ActionResult<CategoryTreeNodeDto> GetCategoryTree()
    {
        var tree = RuriLib.Globals.DescriptorsRepository.AsTree();
        return MapCategoryTreeNode(tree);
    }

    /// <summary>
    /// Gets a block instance by id.
    /// </summary>
    [Admin]
    [HttpGet("block-instance")]
    [MapToApiVersion("1.0")]
    public ActionResult<object> GetBlockInstance(string id)
    {
        BlockInstance block;

        try
        {
            block = BlockFactory.GetBlock<BlockInstance>(id);
        }
        catch (Exception)
        {
            throw new EntryNotFoundException(ErrorCode.InvalidBlockId,
                id, nameof(RuriLib.Globals.DescriptorsRepository));
        }

        // We cast to object to avoid the serializer from
        // serializing the wrong type (base class instead of derived)
        var dto = _mapper.Map<BlockInstanceDto>(block) as object;

        return dto;
    }
    
    /// <summary>
    /// Get the block snippets.
    /// </summary>
    [Admin]
    [HttpGet("block-snippets")]
    [MapToApiVersion("1.0")]
    public ActionResult<FrozenDictionary<string, string>> GetBlockSnippets()
        => _loliCodeAutocompletionService.BlockSnippets;
    
    /// <summary>
    /// Debug a config.
    /// </summary>
    [Admin]
    [HttpPost("debug")]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<DebugConfigResultDto>> Debug(DebugConfigDto dto)
    {
        var options = new DebuggerOptions
        {
            ProxyType = dto.ProxyType,
            TestData = dto.TestData,
            TestProxy = dto.TestProxy ?? string.Empty,
            UseProxy = !string.IsNullOrWhiteSpace(dto.TestProxy),
            WordlistType = dto.WordlistType
        };
        
        ErrorMessage? error = null;

        using var debugger = _configDebuggerService.Create(dto.ConfigId, options);
        
        try
        {
            await debugger.Run();
        }
        catch (Exception ex)
        {
            error = new ErrorMessage {
                Type = ex.GetType().Name, Message = ex.Message, StackTrace = ex.ToString()
            };
        }
        
        return new DebugConfigResultDto
        {
            Log = debugger.Logger.Entries.ToList(),
            Variables = debugger.Options.Variables.Select(ConfigDebuggerService.MapVariable).ToList(),
            Error = error
        };
    }

    private static CategoryTreeNodeDto MapCategoryTreeNode(CategoryTreeNode node)
        => new() {
            Name = node.Name,
            SubCategories = node.SubCategories.Select(MapCategoryTreeNode).ToList(),
            DescriptorIds = node.Descriptors.Select(d => d.Id).ToList()
        };
}
