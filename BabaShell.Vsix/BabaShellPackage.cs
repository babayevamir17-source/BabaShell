using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace BabaShell.Vsix;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("BabaShell", "BabaShell language support", "1.0")]
[ProvideMenuResource("Menus.ctmenu", 1)]
[Guid(PackageGuids.PackageString)]
public sealed class BabaShellPackage : AsyncPackage
{
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        await RunBabaShellCommand.InitializeAsync(this);
    }
}
