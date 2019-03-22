﻿using System.Collections.Generic;
using Automaton.Model.Interfaces;
using Automaton.Model.Modpack.Base;

namespace Automaton.Model.Install.Intefaces
{
    public interface IInstallBase : IService
    {
        string DownloadsDirectory { get; set; }
        string InstallDirectory { get; set; }
        Header ModpackHeader { get; set; }
        List<ExtendedMod> ModpackMods { get; set; }
    }
}