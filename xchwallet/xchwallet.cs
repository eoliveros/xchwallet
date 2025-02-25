﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;

namespace xchwallet
{
    public static class Util
    {
        public const string TYPE_KEY = "Type";
        public const string SEED_KEY = "Seed";
        public const string MAINNET_KEY = "Mainnet";

        public static string GetWalletType(BaseContext db)
        {
            var type = db.CfgGet(TYPE_KEY);
            if (type != null)
                return type.Value;
            return null;
        }

        public static string GetSeed(WalletContext db)
        {
            var type = db.CfgGet(SEED_KEY);
            if (type != null)
                return type.Value;
            return null;  
        }

        public static bool? GetMainnet(WalletContext db)
        {
            if (db.CfgExists(MAINNET_KEY))
                return db.CfgGetBool(MAINNET_KEY, false);
            return null;
        }

        public static void WalletAssert(bool value, string msg)
        {
            if (!value)
                throw new WalletException(msg);
        }
    }

    public class WalletException : Exception
    {
        public WalletException(string msg) : base(msg)
        { }
    }

    public enum LedgerModel
    {
        Account,
        UTXO
    }

    public enum ChainTxStatus
    {
        Unknown,
        Unconfirmed,
        Confirmed,
        DoubleSpent, // possible on UTXO based chains and account based chains that use account nonce     \
        Expired,     // possible account based chains that use timestamp                                   |  txs with these states are not counted when
        Invalid,     // violates a rule (maybe tries to spend funds that are already spent)                |  calculating balance (see ChainTx.Invalid())
        Ignored,     // we want to ignore this tx for whatever reason (maybe so we can double spend it)   /
        
        //!! if you add a new entry make sure to update: ChainTx.Invalid() !!
    }

    public enum WalletDirection
    {
        Incomming, Outgoing
    }

    public enum WalletTxState
    {
        None,
        Seen,
        Ack
    }

    public enum WalletError
    {
        Success,
        MaxFeeBreached,
        InsufficientFunds,
        FailedBroadcast,  // A wallet which can compete an operation with a single transaction might return this error when trying to broadcast it
        PartialBroadcast, // A wallet which might require multiple transactions might return this error
        Cancelled,
        NothingToReplace, // Cannot find the tx to replace
        UnableToReplace,  // Unable to replace (double spend) a tx
    }

    public enum PendingSpendState
    {
        Pending,
        Complete,
        Cancelled,
        Error
    }

    public interface IWallet
    {
        string Type();
        bool IsMainnet();
        LedgerModel GetLedgerModel();
        WalletTag GetTag(string tag);
        IEnumerable<WalletTag> GetTags();
        IEnumerable<WalletTag> GetTagsFor(WalletTx wtx);
        bool HasTag(string tag);
        WalletTag NewTag(string tag);
        WalletAddr NewAddress(string tag);
        WalletAddr NewOrUnusedAddress(string tag);
        WalletAddr NewOrExistingAddress(string tag);
        void UpdateFromBlockchain(IDbContextTransaction dbtx);
        IEnumerable<WalletAddr> GetAddresses(string tag = null);
        IEnumerable<WalletTx> GetTransactions(string tag, string tagFor = null);
        IEnumerable<WalletTx> GetAddrTransactions(string address);
        IEnumerable<ChainTx> GetChainTxs();
        void DeleteTransaction(string txid);
        void SetTransactionStatus(string txid, ChainTxStatus status);
        BigInteger GetBalance(string tag, int minConfs=0);
        BigInteger GetBalance(IEnumerable<string> tags, int minConfs=0);
        BigInteger GetAddrBalance(string address, int minConfs=0);
        WalletPendingSpend RegisterPendingSpend(string tag, string tagChange, string to, BigInteger amount, WalletTag tagFor=null);
        WalletError PendingSpendAction(string spendCode, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<WalletTx> wtxs);
        void PendingSpendCancel(string spendCode);
        IEnumerable<WalletPendingSpend> PendingSpendsGet(string tag = null, IEnumerable<PendingSpendState> states = null, string tagFor=null);
        // feeUnit is wallet specific, in BTC it is satoshis per byte, in ETH it is GWEI per gas, in Waves it is a fixed transaction fee
        WalletError Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<WalletTx> wtxs, WalletTag tagFor=null, string replaceTxId=null);
        WalletError Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<WalletTx> wtxs, int minConfs=0, string ReplaceTxId = null);
        IEnumerable<WalletTx> GetAddrUnacknowledgedTransactions(string address);
        IEnumerable<WalletTx> GetUnacknowledgedTransactions(string tag);
        void SeenTransaction(WalletTx tx);
        void AcknowledgeTransactions(IEnumerable<WalletTx> txs);
        bool ValidateAddress(string address);
        string AmountToString(BigInteger value);
        BigInteger StringToAmount(string value);
        IDbContextTransaction BeginDbTransaction();

        void Save();
    }

    public abstract class BaseWallet : IWallet
    {
        public abstract bool IsMainnet();
        public abstract LedgerModel LedgerModel { get; }
        public abstract WalletAddr NewAddress(string tag);
        public abstract void UpdateFromBlockchain(IDbContextTransaction dbtx);
        public abstract IEnumerable<WalletTx> GetAddrTransactions(string address);
        public abstract BigInteger GetBalance(string tag, int minConfs=0);
        public abstract BigInteger GetAddrBalance(string address, int minConfs=0);
        public abstract WalletError Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<WalletTx> wtxs, WalletTag tagOnBehalfOf=null, string ReplaceTxId = null);
        public abstract WalletError Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<WalletTx> wtxs, int minConfs=0, string ReplaceTxId = null);
        public abstract string AmountToString(BigInteger value);
        public abstract BigInteger StringToAmount(string value);
        public abstract bool ValidateAddress(string address);

        protected abstract string _type();

        protected ILogger logger = null;
        protected WalletContext db = null;
        protected string seedHex = null;
        protected bool mainnet = false;

        public LedgerModel GetLedgerModel()
        {
            return LedgerModel;
        } 

        void CheckType()
        {
            var type = Util.GetWalletType(db);
            if (type == null)
                // newly initialised wallet
                return;
            if (type != Type())
                throw new Exception($"Type found in db ({type}) does not match this wallet class ({Type()})");
        }

        void CheckSeed()
        {
            seedHex = Util.GetSeed(db);
            if (seedHex == null)
            {
                // newly initialised wallet
                logger.LogDebug("New wallet, initializing seed..");
                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                {
                    var data = new byte[256/8];
                    rng.GetBytes(data);
                    seedHex = BitConverter.ToString(data).Replace("-", string.Empty);
                }
            }
        }

        void CheckMainnet()
        {
            var mainnet = Util.GetMainnet(db);
            if (mainnet == null)
                // newly initialised wallet
                return;
            if (mainnet.Value != IsMainnet())
                throw new Exception($"Mainnet found in db ({mainnet.Value}) does not match this wallet class ({IsMainnet()})");
        }

        public BaseWallet(ILogger logger, WalletContext db, bool mainnet)
        {
            this.logger = logger;
            this.db = db;
            this.mainnet = mainnet;
            CheckType();
            CheckSeed();
            CheckMainnet();
        }

        public void Save()
        {
            db.CfgSet(Util.TYPE_KEY, Type());
            db.CfgSet(Util.SEED_KEY, seedHex);
            db.CfgSetBool(Util.MAINNET_KEY, IsMainnet());
            db.SaveChanges();
        }

        public string Type()
        {
            return _type();
        }

        public WalletTag GetTag(string tag)
        {
            return db.WalletTags.SingleOrDefault(t => t.Tag == tag);
        }

        public IEnumerable<WalletTag> GetTags()
        {
            return db.WalletTags;
        }

        public IEnumerable<WalletTag> GetTagsFor(WalletTx wtx)
        {
            return db.TxOutputsForTag.Where(oft => wtx.ChainTx.TxOutputs.Any(to => to.Id == oft.TxOutputId)).Select(oft => oft.Tag).Distinct();
        }

        public bool HasTag(string tag)
        {
            return db.WalletTags.Any(t => t.Tag == tag);
        }

        public WalletTag NewTag(string tag)
        {
            var tag_ = new WalletTag{ Tag = tag };
            db.WalletTags.Add(tag_);
            return tag_;
        }

        public WalletAddr NewOrUnusedAddress(string tag)
        {
            foreach (var addr in GetAddresses(tag))
            {
                var txs = GetAddrTransactions(addr.Address);
                if (!txs.Any())
                    return addr;
            }
            return NewAddress(tag);
        }

        public WalletAddr NewOrExistingAddress(string tag)
        {
            foreach (var addr in GetAddresses(tag))
                return addr;
            return NewAddress(tag);
        }

        public IEnumerable<WalletAddr> GetAddresses(string tag = null)
        {
            return tag != null ? db.AddrsGet(tag) : db.WalletAddrs;
        }

        public IEnumerable<ChainTx> GetChainTxs()
        {
            return db.ChainTxs;
        }

        public IEnumerable<WalletTx> GetTransactions(string tag, string tagFor = null)
        {
            var tagFor_ = tagFor != null ? GetTag(tagFor) : null;
            if (tagFor_ != null)
            {
                var outputsForTag = db.TxOutputsForTag.Where(oft => oft.TagId == tagFor_.Id);
                return db.TxsGet(tag).Where(tx => tx.ChainTx.TxOutputs.Any(to => outputsForTag.Any(oft => oft.TxOutputId == to.Id)));
            }
            return db.TxsGet(tag);
        }

        public void DeleteTransaction(string txid)
        {
            db.TxDelete(txid);
        }

        public void SetTransactionStatus(string txid, ChainTxStatus status)
        {
            var ctx = db.ChainTxGet(txid);
            if (ctx != null)
            {
                if (ctx.NetworkStatus != null)
                {
                    ctx.NetworkStatus.Status = status;
                    db.ChainTxNetworkStatus.Update(ctx.NetworkStatus);
                }
                else
                {
                    ctx.NetworkStatus = new ChainTxNetworkStatus(ctx, status, 0, null);
                    db.ChainTxNetworkStatus.Add(ctx.NetworkStatus);
                }
            }
        }

        public BigInteger GetBalance(IEnumerable<string> tags, int minConfs=0)
        {
            BigInteger amount = 0;
            foreach (var tag in tags)
                amount += this.GetBalance(tag, minConfs);
            return amount;
        }

        public BigInteger GetAddrBalance(WalletAddr addr, int minConfs=0)
        {
            return addr.Balance(minConfs);
        }

        public WalletPendingSpend RegisterPendingSpend(string tag, string tagChange, string to, BigInteger amount, WalletTag tagFor=null)
        {
            if (!ValidateAddress(to))
                return null;
            var spendCode = Utils.CreateDepositCode(allowLetters: true);
            while (db.PendingSpendGet(spendCode) != null)
                spendCode = Utils.CreateDepositCode(allowLetters: true);
            var tag_ = db.TagGet(tag);
            Util.WalletAssert(tag_ != null, $"Tag '{tag}' does not exist");
            var tagChange_ = db.TagGet(tagChange);
            Util.WalletAssert(tagChange_ != null, $"Tag '{tagChange}' does not exist");
            var date = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var spend = new WalletPendingSpend{ SpendCode = spendCode, Date = date, State = PendingSpendState.Pending, Tag = tag_, TagChange = tagChange_, To = to, Amount = amount };
            spend.TagFor = tagFor;
            db.WalletPendingSpends.Add(spend);
            if (tagFor != null)
                db.PendingSpendsForTag.Add(new PendingSpendForTag { PendingSpend = spend, Tag = tagFor });
            return spend;
        }

        public WalletError PendingSpendAction(string spendCode, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<WalletTx> wtxs)
        {
            var spend = db.PendingSpendGet(spendCode);
            Util.WalletAssert(spend != null, $"SpendCode '{spendCode}' does not exist");
            Util.WalletAssert(spend.State == PendingSpendState.Pending || spend.State == PendingSpendState.Error, "PendingSpend has wrong state to action");
            logger.LogDebug($"Actioning pending spend: {spendCode}, from tag: {spend.Tag.Tag}, to: {spend.To}, amount: {spend.Amount}");
            var err = Spend(spend.Tag.Tag, spend.TagChange.Tag, spend.To, spend.Amount, feeMax, feeUnit, out wtxs, spend.TagFor);
            if (err == WalletError.Success)
            {
                spend.State = PendingSpendState.Complete;
                var txids = string.Join(",", wtxs.Select(wtx => wtx.ChainTx.TxId));
                spend.TxIds = txids;
            }
            else
            {
                spend.State = PendingSpendState.Error;
                spend.Error = err;
            }
            db.WalletPendingSpends.Update(spend);
            return err;
        }

        public void PendingSpendCancel(string spendCode)
        {
            var spend = db.PendingSpendGet(spendCode);
            Util.WalletAssert(spend != null, $"SpendCode '{spendCode}' does not exist");
            Util.WalletAssert(spend.State == PendingSpendState.Pending || spend.State == PendingSpendState.Error, "PendingSpend has wrong state to cancel");
            spend.State = PendingSpendState.Cancelled;
            db.WalletPendingSpends.Update(spend);
        }

        public IEnumerable<WalletPendingSpend> PendingSpendsGet(string tag = null, IEnumerable<PendingSpendState> states = null, string tagFor=null)
        {
            if (tag != null)
            {
                var tag_ = db.TagGet(tag);
                if (tag_ == null)
                    return new List<WalletPendingSpend>();
                var q = db.WalletPendingSpends.Where(s => s.TagId == tag_.Id);
                if (states != null)
                    q = db.WalletPendingSpends.Where(s => s.TagId == tag_.Id && states.Contains(s.State));
                var tagFor_ = tagFor != null ? db.TagGet(tagFor) : null;
                if (tagFor_ != null)
                    q = q.Where(s => s.TagFor == tagFor_);
                return q;
            }
            else
            {
                if (states != null)
                    return db.WalletPendingSpends.Where(s => states.Contains(s.State));
                else
                    return db.WalletPendingSpends;
            }
        }

        public IEnumerable<WalletTx> GetAddrUnacknowledgedTransactions(string address)
        {
            return db.TxsUnAckedGet(address);
        }
        
        public IEnumerable<WalletTx> GetUnacknowledgedTransactions(string tag)
        {
            var txs = new List<WalletTx>();
            foreach (var addr in GetAddresses(tag))
            {
                var addrTxs = GetAddrUnacknowledgedTransactions(addr.Address);
                txs.AddRange(addrTxs);
            }
            return txs;
        }

        public void SeenTransaction(WalletTx tx)
        {
            System.Diagnostics.Debug.Assert(tx.State == WalletTxState.None);
            tx.State = WalletTxState.Seen;
            db.WalletTxs.Update(tx);
        }

        public void AcknowledgeTransactions(IEnumerable<WalletTx> txs)
        {
            foreach (var tx in txs)
            {
                System.Diagnostics.Debug.Assert(tx.State != WalletTxState.Ack);
                tx.State = WalletTxState.Ack;
            }
            db.WalletTxs.UpdateRange(txs);
        }

        public IDbContextTransaction BeginDbTransaction()
        {
            return db.Database.BeginTransaction();
        }
    }
}
