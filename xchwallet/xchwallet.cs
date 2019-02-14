﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
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

    public enum WalletDirection
    {
        Incomming, Outgoing
    }

    public enum WalletError
    {
        Success,
        MaxFeeBreached,
        InsufficientFunds,
        FailedBroadcast, // A wallet which can compete an operation with a single transaction might return this error when trying to broadcast it
        PartialBroadcast, // A wallet which might require multiple transactions might return this error
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
        IEnumerable<WalletTag> GetTags();
        WalletTag NewTag(string tag);
        WalletAddr NewAddress(string tag);
        WalletAddr NewOrUnusedAddress(string tag);
        WalletAddr NewOrExistingAddress(string tag);
        void UpdateFromBlockchain();
        IEnumerable<WalletAddr> GetAddresses(string tag);
        IEnumerable<WalletTx> GetTransactions(string tag);
        IEnumerable<WalletTx> GetAddrTransactions(string address);
        BigInteger GetBalance(string tag);
        BigInteger GetAddrBalance(string address);
        WalletPendingSpend RegisterPendingSpend(string tag, string tagChange, string to, BigInteger amount);
        WalletError PendingSpendAction(string spendCode, BigInteger feeMax, BigInteger feeUnit, out WalletTx tx);
        void PendingSpendCancel(string spendCode);
        IEnumerable<WalletPendingSpend> PendingSpendsGet(string tag, IEnumerable<PendingSpendState> states = null);
        // feeUnit is wallet specific, in BTC it is satoshis per byte, in ETH it is GWEI per gas, in Waves it is a fixed transaction fee
        WalletError Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit, out WalletTx wtx, WalletTxMeta meta=null);
        WalletError Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<string> txids);
        IEnumerable<WalletTx> GetAddrUnacknowledgedTransactions(string address);
        IEnumerable<WalletTx> GetUnacknowledgedTransactions(string tag);
        void AcknowledgeTransactions(string tag, IEnumerable<WalletTx> txs);
        void AddNote(string tag, WalletTx wtx, string note);
        void SetTagOnBehalfOf(string tag, WalletTx txid, string tagOnBehalfOf);
        bool ValidateAddress(string address);
        string AmountToString(BigInteger value);
        BigInteger StringToAmount(string value);

        void Save();
    }

    public abstract class BaseWallet : IWallet
    {
        public abstract bool IsMainnet();
        public abstract WalletAddr NewAddress(string tag);
        public abstract void UpdateFromBlockchain();
        public abstract IEnumerable<WalletTx> GetTransactions(string tag);
        public abstract IEnumerable<WalletTx> GetAddrTransactions(string address);
        public abstract BigInteger GetBalance(string tag);
        public abstract BigInteger GetAddrBalance(string address);
        public abstract WalletError Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit, out WalletTx wtx, WalletTxMeta meta=null);
        public abstract WalletError Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<string> txids);
        public abstract string AmountToString(BigInteger value);
        public abstract BigInteger StringToAmount(string value);
        public abstract bool ValidateAddress(string address);

        protected abstract string _type();

        protected ILogger logger = null;
        protected WalletContext db = null;
        protected string seedHex = null;
        protected bool mainnet = false;

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

        public IEnumerable<WalletTag> GetTags()
        {
            return db.WalletTags;
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

        public IEnumerable<WalletAddr> GetAddresses(string tag)
        {
            return db.AddrsGet(tag);
        }

        public WalletPendingSpend RegisterPendingSpend(string tag, string tagChange, string to, BigInteger amount)
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
            db.WalletPendingSpends.Add(spend);
            spend.Meta = new WalletTxMeta();
            db.WalletTxMetas.Add(spend.Meta);
            return spend;
        }

        public WalletError PendingSpendAction(string spendCode, BigInteger feeMax, BigInteger feeUnit, out WalletTx tx)
        {
            var spend = db.PendingSpendGet(spendCode);
            Util.WalletAssert(spend != null, $"SpendCode '{spendCode}' does not exist");
            Util.WalletAssert(spend.State == PendingSpendState.Pending || spend.State == PendingSpendState.Error, "PendingSpend has wrong state to action");
            logger.LogDebug($"Actioning pending spend: {spendCode}, from tag: {spend.Tag.Tag}, to: {spend.To}, amount: {spend.Amount}");
            var err = Spend(spend.Tag.Tag, spend.TagChange.Tag, spend.To, spend.Amount, feeMax, feeUnit, out tx, spend.Meta);
            if (err == WalletError.Success)
            {
                spend.State = PendingSpendState.Complete;
                spend.Tx = tx;
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

        public IEnumerable<WalletPendingSpend> PendingSpendsGet(string tag, IEnumerable<PendingSpendState> states = null)
        {
            var tag_ = db.TagGet(tag);
            if (tag_ == null)
                return new List<WalletPendingSpend>();
            var q = db.WalletPendingSpends.Where(s => s.TagId == tag_.Id);
            if (states != null)
                q = db.WalletPendingSpends.Where(s => s.TagId == tag_.Id && states.Contains(s.State));
            return q;
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

        public void AcknowledgeTransactions(string tag, IEnumerable<WalletTx> txs)
        {
            foreach (var tx in txs)
                tx.Acknowledged = true;
            db.WalletTxs.UpdateRange(txs);
        }

        public void AddNote(string tag, WalletTx wtx, string note)
        {
            wtx.Meta.Note = note;
            db.WalletTxs.Update(wtx);
        }

        public void SetTagOnBehalfOf(string tag, WalletTx wtx, string tagOnBehalfOf)
        {
            wtx.Meta.TagOnBehalfOf = tagOnBehalfOf;
            db.WalletTxs.Update(wtx);
        }
    }
}
