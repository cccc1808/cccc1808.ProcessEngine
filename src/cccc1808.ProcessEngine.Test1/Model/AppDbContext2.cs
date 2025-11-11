//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities;

//using Microsoft.EntityFrameworkCore;

//namespace cccc1808.ProcessEngine.Test1.Model
//{
//    internal class AppDbContext2
//        : DbContext
//    {
//        protected override void OnModelCreating(ModelBuilder modelBuilder)
//        {
//            modelBuilder.Entity<MessageDbEntity<Guid>>(
//                b => 
//                {
//                    b.HasKey(e => e.Id);
//                    b.Property(e => e.Id)
//                        .ValueGeneratedNever();

//                    //b.Property(e => e.IdempotencyId)
//                    //    .HasMaxLength(48);

//                    //b.HasIndex(e => new { e.StreamId, e.IdempotencyId })
//                    //    .IsUnique();
                    
//                    b.HasIndex(e => new { e.StreamId, e.OrderId })
//                        .IsUnique();

//                    // Для обработки.
//                    b.HasIndex(e => new { e.StreamId, e.Priority, e.OrderId });
//                });

//            //modelBuilder.Entity<StreamActiveDbEntity<Guid>>(
//            //    b =>
//            //    {
//            //        b.HasKey(e => e.Id);
//            //        b.Property(e => e.Id)
//            //            .ValueGeneratedNever();
//            //    });

//            //modelBuilder.Entity<StreamProcessDataDbEntity<Guid>>(
//            //    b => 
//            //    {
//            //        b.HasKey(e => e.Id);
//            //        b.Property(e => e.Id)
//            //            .ValueGeneratedNever();

//            //        b.Property(e => e.AggregateId)
//            //            .HasMaxLength(48);

//            //        b.HasIndex(e => e.AggregateId)
//            //            .IsUnique();
//            //    });
//        }
//    }
//}
