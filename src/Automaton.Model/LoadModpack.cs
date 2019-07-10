﻿using Autofac;
using Automaton.Common;
using Automaton.Common.Model;
using Automaton.Model.Interfaces;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Automaton.Model
{
    public class LoadModpack : ILoadModpack
    {
        private readonly IComponentContext _components;

        private readonly IArchiveHandle _archiveHandle;
        private readonly ILifetimeData _lifetimeData;

        public LoadModpack(IComponentContext components)
        {
            _components = components;

            _archiveHandle = components.Resolve<IArchiveHandle>();
            _lifetimeData = components.Resolve<ILifetimeData>();
        }

        public async Task LoadAsync(string modpackPath)
        {
            await Task.Run(() => Load(modpackPath));
        }
        public void Load(string modpackPath)
        {
            var archiveHandle = _archiveHandle.New(modpackPath);
            var archiveEntries = archiveHandle.GetContents().Where(x => !x.IsFolder).ToList();

            var entryMDefinition = archiveEntries.First(x => x.FileName.ToLower() == ConfigPathOffsets.PackDefinitionConfig);

            // Start our prevalidation testing 
            if (entryMDefinition == null)
            {
                return;
            }

            var mDefinitionStream = new MemoryStream();
            entryMDefinition.Extract(mDefinitionStream);

            mDefinitionStream.Seek(0, SeekOrigin.Begin);

            var mDefinition = Utils.LoadJson<MasterDefinition>(mDefinitionStream);

            var modEntries = archiveEntries.Where(x => x.FileName.ToLower().Contains("mods\\"))
                .Where(x => x.FileName.EndsWith(ConfigPathOffsets.InstallConfig)).ToList();
            var mods = new List<Mod>();

            foreach (var entry in modEntries)
            {
                var entryStream = new MemoryStream();
                entry.Extract(entryStream);

                entryStream.Seek(0, SeekOrigin.Begin);

                // This has the chance of spitting out some errors further down the line, will get back to later.
                var modObject = Utils.LoadJson<Mod>(entryStream);

                mods.Add(modObject);
            }

            _lifetimeData.MasterDefinition = mDefinition;
            _lifetimeData.Mods = mods;

            // We also want to grab the fileStreams for required metadata. This includes items like images, ini files and config files. 
            var contentEntries = archiveEntries.Where(x => x.FileName.ToLower().Contains(ConfigPathOffsets.DefaultContentDir)).ToList();

            if (contentEntries.Any())
            {
                var contentItems = contentEntries.Select(x => new ModpackItem()
                {
                    Stream = Utils.GetEntryMemoryStream(x),
                    Name = Path.GetFileName(x.FileName)
                }).ToList();

                _lifetimeData.ModpackContent = contentItems;
            }

            // We want to flip the mods objects so that they're archive-first
            var archives = new List<ExtendedArchive>();

            var patches = archiveHandle.GetContents()
                                       .Where(x => x.FileName.StartsWith("patches\\"))
                                       .ToDictionary(x => Path.GetFileName(x.FileName));


            // Add the MO2 archive. 
            archives.Add(ClassExtensions.ToDerived<SourceArchive, ExtendedArchive>(_lifetimeData.MasterDefinition.MO2Archive)
                                        .Initialize(_components, null, patches, ExtendedArchive.InstallerTypeEnum.ModOrganizer2));

            foreach (var mod in mods)
            {
                var archive = mod.InstallPlans
                    .Select(x => ClassExtensions.ToDerived<SourceArchive, ExtendedArchive>(x.SourceArchive)) // Convert to Extended
                    .Select(x => x.Initialize(_components, mod, patches)) // Initialize each
                    .ToList();

                archives.AddRange(archive);
            }

            _lifetimeData.Archives = archives;
        }
    }
}
