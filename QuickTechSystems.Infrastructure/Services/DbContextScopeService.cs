// QuickTechSystems.Infrastructure/Services/DbContextScopeService.cs
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Application.Interfaces;
using QuickTechSystems.Infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace QuickTechSystems.Infrastructure.Services
{
    public class DbContextScopeService : IDbContextScopeService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public DbContextScopeService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<T> ExecuteInScopeAsync<T>(Func<object, Task<T>> operation)
        {
            using var context = _contextFactory.CreateDbContext();
            return await operation(context);
        }

        public async Task ExecuteInScopeAsync(Func<object, Task> operation)
        {
            using var context = _contextFactory.CreateDbContext();
            await operation(context);
        }
    }
}