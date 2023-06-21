using System.Reflection;


namespace CSharp.SourceGen.Razor;


public readonly struct AppDomainAssemblyResolver : IDisposable
{
    public static AppDomainAssemblyResolver Create(AppDomain appDomain,
        PathByFullNameDictionary pathByFullName)
    {
        var dictionary = pathByFullName.Dictionary;
        var handler = new ResolveEventHandler((_, args) =>
        {
            if (!dictionary.TryGetValue(args.Name, out var path)) return null;

            try
            {
                return Assembly.LoadFrom(path);
            }
            catch
            {
                return null;
            }
        });

        return new AppDomainAssemblyResolver(appDomain, handler);
    }


    private readonly AppDomain _appDomain;
    private readonly ResolveEventHandler _resolveHandler;


    public AppDomainAssemblyResolver(AppDomain appDomain, ResolveEventHandler resolveHandler)
    {
        this._appDomain = appDomain;
        this._resolveHandler = resolveHandler;

        this._appDomain.AssemblyResolve += this._resolveHandler;
    }


    public void Dispose()
    {
        this._appDomain.AssemblyResolve -= this._resolveHandler;
    }
}