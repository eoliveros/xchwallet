﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using xchwallet;

namespace xchwallet.Migrations
{
    [DbContext(typeof(WalletContext))]
    partial class WalletContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.2.1-servicing-10028");

            modelBuilder.Entity("xchwallet.ChainTx", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Amount")
                        .IsRequired()
                        .HasColumnType("string");

                    b.Property<long>("Confirmations");

                    b.Property<long>("Date");

                    b.Property<string>("Fee")
                        .IsRequired()
                        .HasColumnType("string");

                    b.Property<string>("From");

                    b.Property<string>("To");

                    b.Property<string>("TxId");

                    b.HasKey("Id");

                    b.HasIndex("TxId")
                        .IsUnique();

                    b.ToTable("ChainTxs");
                });

            modelBuilder.Entity("xchwallet.WalletAddr", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Address");

                    b.Property<string>("Path");

                    b.Property<int>("PathIndex");

                    b.Property<int>("TagId");

                    b.HasKey("Id");

                    b.HasIndex("TagId");

                    b.ToTable("WalletAddrs");
                });

            modelBuilder.Entity("xchwallet.WalletCfg", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Key");

                    b.Property<string>("Value");

                    b.HasKey("Id");

                    b.HasIndex("Key")
                        .IsUnique();

                    b.ToTable("WalletCfgs");
                });

            modelBuilder.Entity("xchwallet.WalletTag", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Tag");

                    b.HasKey("Id");

                    b.ToTable("WalletTags");
                });

            modelBuilder.Entity("xchwallet.WalletTx", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<bool>("Acknowledged");

                    b.Property<int>("ChainTxId");

                    b.Property<int>("Direction");

                    b.Property<string>("Note");

                    b.Property<string>("TagOnBehalfOf");

                    b.Property<int>("WalletAddrId");

                    b.Property<long>("WalletId");

                    b.HasKey("Id");

                    b.HasIndex("ChainTxId");

                    b.HasIndex("WalletAddrId");

                    b.ToTable("WalletTxs");
                });

            modelBuilder.Entity("xchwallet.WalletAddr", b =>
                {
                    b.HasOne("xchwallet.WalletTag", "Tag")
                        .WithMany("Addrs")
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("xchwallet.WalletTx", b =>
                {
                    b.HasOne("xchwallet.ChainTx", "ChainTx")
                        .WithMany()
                        .HasForeignKey("ChainTxId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("xchwallet.WalletAddr", "Address")
                        .WithMany("Txs")
                        .HasForeignKey("WalletAddrId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
#pragma warning restore 612, 618
        }
    }
}
