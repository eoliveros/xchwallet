﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;

namespace xchwallet
{
    public static class Util
    {
        public const string TYPE_KEY = "Type";

        public static string GetWalletType(WalletContext db)
        {
            var type = db.CfgGet(TYPE_KEY);
            if (type != null)
                return type.Value;
            return null;
        }
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

    public interface IWallet
    {
        string Type();
        bool IsMainnet();
        IEnumerable<WalletTag> GetTags();
        WalletAddr NewAddress(string tag);
        WalletAddr NewOrUnusedAddress(string tag);
        void UpdateFromBlockchain();
        IEnumerable<WalletAddr> GetAddresses(string tag);
        IEnumerable<WalletTx> GetTransactions(string tag);
        IEnumerable<WalletTx> GetAddrTransactions(string address);
        BigInteger GetBalance(string tag);
        BigInteger GetAddrBalance(string address);
        // feeUnit is wallet specific, in BTC it is satoshis per byte, in ETH it is GWEI per gas, in Waves it is a fixed transaction fee
        WalletError Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<string> txids);
        WalletError Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<string> txids);
        IEnumerable<WalletTx> GetAddrUnacknowledgedTransactions(string address);
        IEnumerable<WalletTx> GetUnacknowledgedTransactions(string tag);
        void AcknowledgeTransactions(string tag, IEnumerable<WalletTx> txs);
        void AddNote(string tag, IEnumerable<string> txids, string note);
        void AddNote(string tag, string txid, string note);
        void SetTagOnBehalfOf(string tag, IEnumerable<string> txids, string tagOnBehalfOf);
        void SetTagOnBehalfOf(string tag, string txid, string tagOnBehalfOf);
        void SetTxWalletId(string tag, IEnumerable<string> txids, long id);
        void SetTxWalletId(string tag, string txid, long id);
        long GetNextTxWalletId(string tag);
        bool ValidateAddress(string address);
        string AmountToString(BigInteger value);
        BigInteger StringToAmount(string value);

        void Save();
    }

    public abstract class BaseWallet : IWallet
    {
        public abstract string Type();
        public abstract bool IsMainnet();
        public abstract WalletAddr NewAddress(string tag);
        public abstract void UpdateFromBlockchain();
        public abstract IEnumerable<WalletTx> GetTransactions(string tag);
        public abstract IEnumerable<WalletTx> GetAddrTransactions(string address);
        public abstract BigInteger GetBalance(string tag);
        public abstract BigInteger GetAddrBalance(string address);
        public abstract WalletError Spend(string tag, string tagChange, string to, BigInteger amount, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<string> txids);
        public abstract WalletError Consolidate(IEnumerable<string> tagFrom, string tagTo, BigInteger feeMax, BigInteger feeUnit, out IEnumerable<string> txids);
        public abstract string AmountToString(BigInteger value);
        public abstract BigInteger StringToAmount(string value);
        public abstract bool ValidateAddress(string address);

        protected WalletContext db = null;

        void CheckType()
        {
            var type = db.CfgGet(Util.TYPE_KEY);
            if (type == null)
                // newly initialised wallet
                return;
            if (type.Value != Type())
                throw new Exception($"Type found in db ({type.Value}) does not match this wallet class ({Type()})");
        }

        public BaseWallet(WalletContext db)
        {
            this.db = db;
            CheckType();
        }

        public void Save()
        {
            db.CfgSet(Util.TYPE_KEY, Type());
            db.SaveChanges();
        }

        public IEnumerable<WalletTag> GetTags()
        {
            return db.WalletTags;
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

        public IEnumerable<WalletAddr> GetAddresses(string tag)
        {
            return db.AddrsGet(tag);
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

        public void AddNote(string tag, IEnumerable<string> txids, string note)
        {
            foreach (var tx in GetTransactions(tag))
                if (txids.Contains(tx.ChainTx.TxId))
                {
                    tx.Note = note;
                    db.WalletTxs.Update(tx);
                }
        }

        public void AddNote(string tag, string txid, string note)
        {
            foreach (var tx in GetTransactions(tag))
                if (tx.ChainTx.TxId == txid)
                {
                    tx.Note = note;
                    db.WalletTxs.Update(tx);
                    break;
                }
        }

        public void SetTagOnBehalfOf(string tag, IEnumerable<string> txids, string tagOnBehalfOf)
        {
            foreach (var tx in GetTransactions(tag))
                if (txids.Contains(tx.ChainTx.TxId))
                {
                    tx.TagOnBehalfOf = tagOnBehalfOf;
                    db.WalletTxs.Update(tx);
                }
        }

        public void SetTagOnBehalfOf(string tag, string txid, string tagOnBehalfOf)
        {
            foreach (var tx in GetTransactions(tagOnBehalfOf))
                if (tx.ChainTx.TxId == txid)
                {
                    tx.TagOnBehalfOf = tagOnBehalfOf;
                    db.WalletTxs.Update(tx);
                    break;
                }
        }

        public void SetTxWalletId(string tag, IEnumerable<string> txids, long id)
        {
            foreach (var tx in GetTransactions(tag))
                if (txids.Contains(tx.ChainTx.TxId))
                {
                    tx.WalletId = id;
                    db.WalletTxs.Update(tx);
                }
        }

        public void SetTxWalletId(string tag, string txid, long id)
        {
            foreach (var tx in GetTransactions(tag))
                if (tx.ChainTx.TxId == txid)
                {
                    tx.WalletId = id;
                    db.WalletTxs.Update(tx);
                    break;
                }
        }
                
        public long GetNextTxWalletId(string tag)
        {
            long res = 0;
            foreach (var tx in GetTransactions(tag))
                if (tx.WalletId > res)
                    res = tx.WalletId;
            return res + 1;
        }
    }
}
