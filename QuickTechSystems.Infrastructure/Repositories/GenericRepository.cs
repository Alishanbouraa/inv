// QuickTechSystems.Infrastructure.Repositories/GenericRepository.cs
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Domain.Interfaces.Repositories;
using QuickTechSystems.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuickTechSystems.Infrastructure.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;
        protected readonly IDbContextFactory<ApplicationDbContext>? _contextFactory;

        public GenericRepository(ApplicationDbContext context, IDbContextFactory<ApplicationDbContext>? contextFactory = null)
        {
            _context = context;
            _contextFactory = contextFactory;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            try
            {
                return await _dbSet.FindAsync(id);
            }
            catch (ObjectDisposedException)
            {
                if (_contextFactory != null)
                {
                    // Use a fresh context for this operation
                    using var freshContext = _contextFactory.CreateDbContext();
                    return await freshContext.Set<T>().FindAsync(id);
                }
                throw;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("disposed") || ex.Message.Contains("second operation"))
            {
                if (_contextFactory != null)
                {
                    // Use a fresh context for this operation
                    using var freshContext = _contextFactory.CreateDbContext();
                    return await freshContext.Set<T>().FindAsync(id);
                }
                throw;
            }
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            try
            {
                return await _dbSet.ToListAsync();
            }
            catch (ObjectDisposedException)
            {
                if (_contextFactory != null)
                {
                    // Use a fresh context for this operation
                    using var freshContext = _contextFactory.CreateDbContext();
                    return await freshContext.Set<T>().ToListAsync();
                }
                throw;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("disposed") || ex.Message.Contains("second operation"))
            {
                if (_contextFactory != null)
                {
                    // Use a fresh context for this operation
                    using var freshContext = _contextFactory.CreateDbContext();
                    return await freshContext.Set<T>().ToListAsync();
                }
                throw;
            }
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            try
            {
                await _dbSet.AddAsync(entity);
                return entity;
            }
            catch (ObjectDisposedException)
            {
                if (_contextFactory != null)
                {
                    // Get a new context if the current one is disposed
                    _context = _contextFactory.CreateDbContext();
                    _dbSet.Attach(entity);
                    _context.Entry(entity).State = EntityState.Added;
                    return entity;
                }
                throw;
            }
        }

        public virtual Task UpdateAsync(T entity)
        {
            try
            {
                _context.Entry(entity).State = EntityState.Modified;
                return Task.CompletedTask;
            }
            catch (ObjectDisposedException)
            {
                if (_contextFactory != null)
                {
                    // Get a new context if the current one is disposed
                    _context = _contextFactory.CreateDbContext();
                    _dbSet.Attach(entity);
                    _context.Entry(entity).State = EntityState.Modified;
                    return Task.CompletedTask;
                }
                throw;
            }
        }

        public virtual Task DeleteAsync(T entity)
        {
            try
            {
                _dbSet.Remove(entity);
                return Task.CompletedTask;
            }
            catch (ObjectDisposedException)
            {
                if (_contextFactory != null)
                {
                    // Get a new context if the current one is disposed
                    _context = _contextFactory.CreateDbContext();
                    _dbSet.Attach(entity);
                    _context.Entry(entity).State = EntityState.Deleted;
                    return Task.CompletedTask;
                }
                throw;
            }
        }

        public virtual IQueryable<T> Query()
        {
            try
            {
                return _dbSet.AsQueryable();
            }
            catch (ObjectDisposedException)
            {
                if (_contextFactory != null)
                {
                    // Use a fresh context for this operation
                    _context = _contextFactory.CreateDbContext();
                    return _context.Set<T>().AsQueryable();
                }
                throw;
            }
        }
    }
}