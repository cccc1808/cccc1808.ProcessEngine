using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Test1.Model.InboxOutbox
{
    internal class AppDbContext
        : DbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProcessTypeEntity>(
                b =>
                {
                    b.HasKey(e => e.Id);
                    b.Property(e => e.Id).ValueGeneratedNever();

                    b.Property(e => e.Name)
                        .HasMaxLength(255);

                    b.HasIndex(e => new { e.Name, e.Version })
                        .IncludeProperties(e => e.Id)
                        .IsUnique();

                    b.HasData(
                        new ProcessTypeEntity()
                        {
                            Id = 0,
                            Name = "Process1",
                            Version = 0
                        }
                        );
                });

            modelBuilder.Entity<ProcessErrorDbEntity<Guid>>(
                b =>
                {
                    b.HasKey(e => e.Id);
                    b.Property(e => e.Id).ValueGeneratedNever();
                });

            //-------------------------------------

            modelBuilder.Entity<MessageDbEntity<Guid>>(
                b => 
                {
                    
                });

            //modelBuilder.Entity<StreamActiveDbEntity<Guid>>(
            //    b =>
            //    {

            //    });

            //-------------------------------------
        }
    }
}
