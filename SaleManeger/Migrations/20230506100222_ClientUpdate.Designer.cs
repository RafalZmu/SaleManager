﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SaleManeger.Models;

#nullable disable

namespace SaleManeger.Migrations
{
    [DbContext(typeof(SaleContext))]
    [Migration("20230506100222_ClientUpdate")]
    partial class ClientUpdate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.5");

            modelBuilder.Entity("SaleManeger.Models.Client", b =>
                {
                    b.Property<string>("ID")
                        .HasColumnType("TEXT");

                    b.Property<string>("Color")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<string>("PhoneNumber")
                        .HasColumnType("TEXT");

                    b.HasKey("ID");

                    b.ToTable("Clients");
                });

            modelBuilder.Entity("SaleManeger.Models.ClientOrder", b =>
                {
                    b.Property<string>("ClientOrderID")
                        .HasColumnType("TEXT");

                    b.Property<string>("ClientID")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("Date")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsReserved")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ProductID")
                        .HasColumnType("TEXT");

                    b.Property<string>("SaleID")
                        .HasColumnType("TEXT");

                    b.Property<string>("Value")
                        .HasColumnType("TEXT");

                    b.HasKey("ClientOrderID");

                    b.ToTable("ClientsOrders");
                });

            modelBuilder.Entity("SaleManeger.Models.Product", b =>
                {
                    b.Property<string>("ID")
                        .HasColumnType("TEXT");

                    b.Property<string>("Code")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsReserved")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<double>("PricePerKg")
                        .HasColumnType("REAL");

                    b.Property<string>("Value")
                        .HasColumnType("TEXT");

                    b.HasKey("ID");

                    b.ToTable("Products");
                });

            modelBuilder.Entity("SaleManeger.Models.Sale", b =>
                {
                    b.Property<string>("SaleID")
                        .HasColumnType("TEXT");

                    b.Property<string>("SaleID")
                        .HasColumnType("TEXT");

                    b.HasKey("SaleID");

                    b.ToTable("Sales");
                });
#pragma warning restore 612, 618
        }
    }
}
