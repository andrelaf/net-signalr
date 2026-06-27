using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SignalRDemo.Infrastructure.Data;

/// <summary>
/// Usado apenas pelas ferramentas do EF Core (`dotnet ef migrations`) em design time.
/// Mantém a geração de migrations desacoplada da configuração do host da API.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=workspace.db")
            .Options;
        return new AppDbContext(options);
    }
}
