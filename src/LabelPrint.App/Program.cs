using LabelPrint.CardReader;
using LabelPrint.Data;
using LabelPrint.Printing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LabelPrint;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory
            });

            // Local file and user secrets sit under appsettings.json. Environment and the command line still win.
            builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
            builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
            builder.Configuration.AddEnvironmentVariables();
            if (args.Length > 0)
                builder.Configuration.AddCommandLine(args);

            try
            {
                builder.Logging.AddProvider(new FileLoggerProvider());
            }
            catch (IOException)
            {
                // The desk can still run when the log folder is not writable.
            }

            builder.Services.Configure<LabelPrintOptions>(builder.Configuration.GetSection(LabelPrintOptions.SectionName));
            builder.Services.Configure<MisConnectionOptions>(builder.Configuration.GetSection(MisConnectionOptions.SectionName));
            builder.Services.AddSingleton<MisConnectionFactory>();
            builder.Services.AddSingleton<ILeagerRepository, LeagerRepository>();
            builder.Services.AddSingleton<IMtUserRepository, MtUserRepository>();
            builder.Services.AddSingleton<ILabelTemplateCatalog>(services =>
            {
                var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<LabelPrintOptions>>().Value;
                return new LabelTemplateCatalog(AppContext.BaseDirectory, options.TemplateDirectory);
            });
            builder.Services.AddSingleton<INameplatePrintPipeline, NameplatePrintPipeline>();
            builder.Services.AddSingleton<ICardReader, CardReaderService>();
            builder.Services.AddTransient<MainForm>();

            using var host = builder.Build();
            Application.Run(host.Services.GetRequiredService<MainForm>());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, DeskMessages.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
