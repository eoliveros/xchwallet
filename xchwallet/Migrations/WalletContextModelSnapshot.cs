﻿// <auto-generated />
using System;
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
                .HasAnnotation("ProductVersion", "2.2.1-servicing-10028")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("xchwallet.BalanceUpdate", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Amount")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<int>("ChainTxId");

                    b.Property<string>("From");

                    b.Property<bool>("Input");

                    b.Property<uint>("N");

                    b.Property<string>("To");

                    b.Property<string>("TxId");

                    b.Property<int>("WalletAddrId");

                    b.HasKey("Id");

                    b.HasIndex("ChainTxId");

                    b.HasIndex("WalletAddrId");

                    b.ToTable("BalanceUpdates");
                });

            modelBuilder.Entity("xchwallet.ChainAttachment", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int>("ChainTxId");

                    b.Property<byte[]>("Data");

                    b.HasKey("Id");

                    b.HasIndex("ChainTxId")
                        .IsUnique();

                    b.ToTable("ChainAttachments");
                });

            modelBuilder.Entity("xchwallet.ChainTx", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<long>("Confirmations");

                    b.Property<long>("Date");

                    b.Property<string>("Fee")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<long>("Height");

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

                    b.HasIndex("Address")
                        .IsUnique();

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

            modelBuilder.Entity("xchwallet.WalletPendingSpend", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Amount")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<long>("Date");

                    b.Property<int>("Error");

                    b.Property<string>("ErrorMessage");

                    b.Property<string>("SpendCode");

                    b.Property<int>("State");

                    b.Property<int>("TagChangeId");

                    b.Property<int>("TagId");

                    b.Property<string>("To");

                    b.Property<int?>("WalletTxId");

                    b.Property<int>("WalletTxMetaId");

                    b.HasKey("Id");

                    b.HasIndex("SpendCode")
                        .IsUnique();

                    b.HasIndex("TagChangeId");

                    b.HasIndex("TagId");

                    b.HasIndex("WalletTxId");

                    b.HasIndex("WalletTxMetaId");

                    b.ToTable("WalletPendingSpends");
                });

            modelBuilder.Entity("xchwallet.WalletTag", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Tag");

                    b.HasKey("Id");

                    b.HasIndex("Tag")
                        .IsUnique();

                    b.ToTable("WalletTags");
                });

            modelBuilder.Entity("xchwallet.WalletTx", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<bool>("Acknowledged");

                    b.Property<int>("ChainTxId");

                    b.Property<int>("Direction");

                    b.Property<int>("WalletAddrId");

                    b.Property<int>("WalletTxMetaId");

                    b.HasKey("Id");

                    b.HasIndex("ChainTxId");

                    b.HasIndex("WalletAddrId");

                    b.HasIndex("WalletTxMetaId");

                    b.ToTable("WalletTxs");
                });

            modelBuilder.Entity("xchwallet.WalletTxMeta", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Note");

                    b.Property<string>("TagOnBehalfOf");

                    b.HasKey("Id");

                    b.ToTable("WalletTxMetas");
                });

            modelBuilder.Entity("xchwallet.BalanceUpdate", b =>
                {
                    b.HasOne("xchwallet.ChainTx", "ChainTx")
                        .WithMany("BalanceUpdates")
                        .HasForeignKey("ChainTxId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("xchwallet.WalletAddr", "WalletAddr")
                        .WithMany("BalanceUpdates")
                        .HasForeignKey("WalletAddrId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("xchwallet.ChainAttachment", b =>
                {
                    b.HasOne("xchwallet.ChainTx", "Tx")
                        .WithOne("Attachment")
                        .HasForeignKey("xchwallet.ChainAttachment", "ChainTxId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("xchwallet.WalletAddr", b =>
                {
                    b.HasOne("xchwallet.WalletTag", "Tag")
                        .WithMany("Addrs")
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("xchwallet.WalletPendingSpend", b =>
                {
                    b.HasOne("xchwallet.WalletTag", "TagChange")
                        .WithMany()
                        .HasForeignKey("TagChangeId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("xchwallet.WalletTag", "Tag")
                        .WithMany()
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("xchwallet.WalletTx", "Tx")
                        .WithMany()
                        .HasForeignKey("WalletTxId");

                    b.HasOne("xchwallet.WalletTxMeta", "Meta")
                        .WithMany()
                        .HasForeignKey("WalletTxMetaId")
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

                    b.HasOne("xchwallet.WalletTxMeta", "Meta")
                        .WithMany()
                        .HasForeignKey("WalletTxMetaId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
#pragma warning restore 612, 618
        }
    }
}
