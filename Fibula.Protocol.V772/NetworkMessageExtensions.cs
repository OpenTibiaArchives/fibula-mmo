﻿// -----------------------------------------------------------------
// <copyright file="NetworkMessageExtensions.cs" company="2Dudes">
// Copyright (c) | Jose L. Nunez de Caceres et al.
// https://linkedin.com/in/nunezdecaceres
//
// All Rights Reserved.
//
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------

namespace Fibula.Protocol.V772
{
    using System;
    using Fibula.Common.Contracts.Enumerations;
    using Fibula.Common.Contracts.Extensions;
    using Fibula.Common.Contracts.Structs;
    using Fibula.Common.Utilities;
    using Fibula.Communications.Contracts.Abstractions;
    using Fibula.Communications.Contracts.Enumerations;
    using Fibula.Creatures.Contracts.Abstractions;
    using Fibula.Data.Entities.Contracts.Structs;
    using Fibula.Items.Contracts.Abstractions;
    using Fibula.Security.Encryption;

    /// <summary>
    /// Static class that defines extension methods for an <see cref="INetworkMessage"/>.
    /// </summary>
    public static class NetworkMessageExtensions
    {
        /// <summary>
        /// Attempts to decrypt the message using XTea keys.
        /// </summary>
        /// <param name="message">The message to act on.</param>
        /// <param name="key">The key to use.</param>
        /// <returns>True if the message could be decrypted, false otherwise.</returns>
        public static bool XteaDecrypt(this INetworkMessage message, uint[] key)
        {
            var targetSpan = message.Buffer[0..message.Length];

            var result = Xtea.Decrypt(targetSpan, key, out int newMessageLen);

            if (result)
            {
                message.Resize(newMessageLen, resetCursor: false);
            }

            return result;
        }

        /// <summary>
        /// Attempts to encrypt the message using XTea keys.
        /// </summary>
        /// <param name="message">The message to act on.</param>
        /// <param name="key">The XTEA key.</param>
        /// <returns>True if the encryption succeeds, false otherwise.</returns>
        public static bool XteaEncrypt(this INetworkMessage message, uint[] key)
        {
            var targetSpan = message.Buffer[2..message.Length];

            var result = Xtea.Encrypt(targetSpan, key, out int newMessageLen);

            if (result)
            {
                message.Resize(newMessageLen + 2, resetCursor: false);
            }

            return result;
        }

        /// <summary>
        /// Prepares an <see cref="INetworkMessage"/> to be sent, encrypting it with the supplied key.
        /// </summary>
        /// <param name="message">The message to prepare.</param>
        /// <param name="xteaKey">The XTea key to encrypt with.</param>
        /// <returns>True if the message is prepared successfully, false otherwise.</returns>
        public static bool PrepareToSend(this INetworkMessage message, uint[] xteaKey)
        {
            // Must be before Xtea, because the packet length is encrypted as well
            message.InsertPacketLength();

            // if (!Xtea.Encrypt(ref message.buffer, ref message.length, 2, xteaKey))
            if (!message.XteaEncrypt(xteaKey))
            {
                return false;
            }

            // Must be after Xtea, because takes the checksum of the encrypted packet
            // InsertAdler32();
            message.InsertTotalLength();

            return true;
        }

        /// <summary>
        /// Adds a <see cref="Location"/>'s description to the message.
        /// </summary>
        /// <param name="message">The message to add the location to.</param>
        /// <param name="location">The location to add.</param>
        public static void AddLocation(this INetworkMessage message, Location location)
        {
            message.AddUInt16((ushort)location.X);
            message.AddUInt16((ushort)location.Y);
            message.AddByte((byte)location.Z);
        }

        /// <summary>
        /// Add a <see cref="ICreature"/>'s description to the message.
        /// </summary>
        /// <param name="message">The message to add the creature description to.</param>
        /// <param name="creature">The creature to describe and add.</param>
        /// <param name="asKnown">A value indicating whether this creature is known.</param>
        /// <param name="creatureToRemoveId">The id of another creature to replace if the client buffer is known to be full.</param>
        public static void AddCreature(this INetworkMessage message, ICreature creature, bool asKnown, uint creatureToRemoveId)
        {
            if (asKnown)
            {
                message.AddUInt16((byte)OutgoingGamePacketType.AddKnownCreature);
                message.AddUInt32(creature.Id);
            }
            else
            {
                message.AddUInt16((byte)OutgoingGamePacketType.AddUnknownCreature);
                message.AddUInt32(creatureToRemoveId);
                message.AddUInt32(creature.Id);
                message.AddString(creature.Name);
            }

            message.AddByte(creature.HitpointPercentage);
            message.AddByte(Convert.ToByte(creature.Direction.GetClientSafeDirection()));

            /* if (creature.IsInvisible)
            {
                message.AddUInt16(0x00);
                message.AddUInt16(0x00);
            }
            else */
            {
                message.AddOutfit(creature.Outfit);
            }

            message.AddByte(creature.EmittedLightLevel);
            message.AddByte(creature.EmittedLightColor);
            message.AddUInt16(creature.Speed);

            message.AddByte(0x00); // Skull
            message.AddByte(0x00); // Shield
        }

        /// <summary>
        /// Adds an <see cref="Outfit"/>'s description to the message.
        /// </summary>
        /// <param name="message">The message to add the outfit to.</param>
        /// <param name="outfit">The outfit to add.</param>
        public static void AddOutfit(this INetworkMessage message, Outfit outfit)
        {
            // Add creature outfit
            message.AddUInt16(outfit.Id);

            if (outfit.Id > 0)
            {
                message.AddByte(outfit.Head);
                message.AddByte(outfit.Body);
                message.AddByte(outfit.Legs);
                message.AddByte(outfit.Feet);
            }
            else
            {
                message.AddUInt16(outfit.ItemIdLookAlike);
            }
        }

        /// <summary>
        /// Adds an <see cref="IItem"/>'s description to a message.
        /// </summary>
        /// <param name="message">The message to add the item description to.</param>
        /// <param name="item">The item to describe and add.</param>
        public static void AddItem(this INetworkMessage message, IItem item)
        {
            item.ThrowIfNull(nameof(item));

            message.AddUInt16(item.Type.ClientId);

            if (item.IsCumulative)
            {
                message.AddByte(item.Amount);
            }
            else if (item.IsLiquidPool || item.IsLiquidSource || item.IsLiquidContainer)
            {
                message.AddByte((byte)item.LiquidType.ToLiquidColor());
            }
        }

        /// <summary>
        /// Converts a <see cref="LiquidType"/> to the client supported <see cref="LiquidColor"/>.
        /// </summary>
        /// <param name="liquidType">The type of liquid.</param>
        /// <returns>The color supported by the client.</returns>
        public static LiquidColor ToLiquidColor(this LiquidType liquidType)
        {
            switch (liquidType)
            {
                default:
                case LiquidType.None:
                    return LiquidColor.None;
                case LiquidType.Water:
                    return LiquidColor.Blue;
                case LiquidType.Wine:
                case LiquidType.ManaFluid:
                    return LiquidColor.Purple;
                case LiquidType.Beer:
                case LiquidType.Mud:
                case LiquidType.Oil:
                    return LiquidColor.Brown;
                case LiquidType.Blood:
                case LiquidType.LifeFluid:
                    return LiquidColor.Red;
                case LiquidType.Slime:
                case LiquidType.Lemonade:
                    return LiquidColor.Green;
                case LiquidType.Urine:
                    return LiquidColor.Yellow;
                case LiquidType.Milk:
                    return LiquidColor.White;
            }
        }

        private static void InsertPacketLength(this INetworkMessage message)
        {
            var lenBytes = BitConverter.GetBytes((ushort)(message.Length - 4));

            message.Buffer[2] = lenBytes[0];
            message.Buffer[3] = lenBytes[1];
        }

        private static void InsertTotalLength(this INetworkMessage message)
        {
            var lenBytes = BitConverter.GetBytes((ushort)(message.Length - 2));

            message.Buffer[0] = lenBytes[0];
            message.Buffer[1] = lenBytes[1];
        }
    }
}
