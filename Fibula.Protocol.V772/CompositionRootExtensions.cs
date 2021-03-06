﻿// -----------------------------------------------------------------
// <copyright file="CompositionRootExtensions.cs" company="2Dudes">
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
    using System.Collections.Generic;
    using Fibula.Common.Utilities;
    using Fibula.Communications.Contracts.Abstractions;
    using Fibula.Communications.Contracts.Enumerations;
    using Fibula.Communications.Listeners;
    using Fibula.Items.Contracts.Abstractions;
    using Fibula.Map.Contracts.Abstractions;
    using Fibula.Protocol.V772.PacketReaders;
    using Fibula.Protocol.V772.PacketWriters;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Options;
    using Serilog;

    /// <summary>
    /// Static class that adds convenient methods to add the concrete implementations contained in this library.
    /// </summary>
    public static class CompositionRootExtensions
    {
        /// <summary>
        /// Adds all the game server components related to protocol 7.72 contained in this library to the services collection.
        /// It also configures any <see cref="IOptions{T}"/> required by any such components.
        /// </summary>
        /// <param name="services">The services collection.</param>
        /// <param name="configuration">The configuration loaded.</param>
        public static void AddProtocol772GameServerComponents(this IServiceCollection services, IConfiguration configuration)
        {
            configuration.ThrowIfNull(nameof(configuration));

            // Configure the options required by the services we're about to add.
            services.Configure<GameListenerOptions>(configuration.GetSection(nameof(GameListenerOptions)));

            // Add all handlers
            services.TryAddSingleton<GameLogInPacketReader>();

            var packetReadersToAdd = new Dictionary<IncomingGamePacketType, Type>()
            {
                { IncomingGamePacketType.Attack, typeof(AttackPacketReader) },
                { IncomingGamePacketType.AutoMove, typeof(AutoMovePacketReader) },
                { IncomingGamePacketType.AutoMoveCancel, typeof(AutoMoveCancelPacketReader) },
                { IncomingGamePacketType.ChangeModes, typeof(ChangeModesPacketReader) },
                { IncomingGamePacketType.Follow, typeof(FollowPacketReader) },
                { IncomingGamePacketType.Heartbeat, typeof(HeartbeatPacketReader) },
                { IncomingGamePacketType.HeartbeatResponse, typeof(HeartbeatResponsePacketReader) },
                { IncomingGamePacketType.LogIn, typeof(GameLogInPacketReader) },
                { IncomingGamePacketType.LogOut, typeof(GameLogOutPacketReader) },
                { IncomingGamePacketType.LookAt, typeof(LookAtPacketReader) },
                { IncomingGamePacketType.Speech, typeof(SpeechPacketReader) },
                { IncomingGamePacketType.StopAllActions, typeof(StopAllActionsPacketReader) },
                { IncomingGamePacketType.TurnNorth, typeof(TurnNorthPacketReader) },
                { IncomingGamePacketType.TurnEast, typeof(TurnEastPacketReader) },
                { IncomingGamePacketType.TurnSouth, typeof(TurnSouthPacketReader) },
                { IncomingGamePacketType.TurnWest, typeof(TurnWestPacketReader) },
                { IncomingGamePacketType.WalkNorth, typeof(WalkNorthPacketReader) },
                { IncomingGamePacketType.WalkNortheast, typeof(WalkNortheastPacketReader) },
                { IncomingGamePacketType.WalkEast, typeof(WalkEastPacketReader) },
                { IncomingGamePacketType.WalkSoutheast, typeof(WalkSoutheastPacketReader) },
                { IncomingGamePacketType.WalkSouth, typeof(WalkSouthPacketReader) },
                { IncomingGamePacketType.WalkSouthwest, typeof(WalkSouthwestPacketReader) },
                { IncomingGamePacketType.WalkWest, typeof(WalkWestPacketReader) },
                { IncomingGamePacketType.WalkNorthwest, typeof(WalkNorthwestPacketReader) },
            };

            foreach (var (packetType, type) in packetReadersToAdd)
            {
                services.TryAddSingleton(type);
            }

            var packetWritersToAdd = new Dictionary<OutgoingGamePacketType, Type>()
            {
                { OutgoingGamePacketType.AnimatedText, typeof(AnimatedTextPacketWriter) },
                { OutgoingGamePacketType.AddThing, typeof(AddCreaturePacketWriter) },
                { OutgoingGamePacketType.ContainerClose, typeof(ContainerClosePacketWriter) },
                { OutgoingGamePacketType.ContainerOpen, typeof(ContainerOpenPacketWriter) },
                { OutgoingGamePacketType.ContainerAddItem, typeof(ContainerAddItemPacketWriter) },
                { OutgoingGamePacketType.ContainerRemoveItem, typeof(ContainerRemoveItemPacketWriter) },
                { OutgoingGamePacketType.ContainerUpdateItem, typeof(ContainerUpdateItemPacketWriter) },
                { OutgoingGamePacketType.CreatureHealth, typeof(CreatureHealthPacketWriter) },
                { OutgoingGamePacketType.CreatureLight, typeof(CreatureLightPacketWriter) },
                { OutgoingGamePacketType.CreatureMoved, typeof(CreatureMovedPacketWriter) },
                { OutgoingGamePacketType.UpdateThing, typeof(CreatureTurnedPacketWriter) },
                { OutgoingGamePacketType.CreatureSpeech, typeof(CreatureSpeechPacketWriter) },
                { OutgoingGamePacketType.Disconnect, typeof(GameServerDisconnectPacketWriter) },
                { OutgoingGamePacketType.Heartbeat, typeof(HeartbeatPacketWriter) },
                { OutgoingGamePacketType.HeartbeatResponse, typeof(HeartbeatResponsePacketWriter) },
                { OutgoingGamePacketType.MagicEffect, typeof(MagicEffectPacketWriter) },
                { OutgoingGamePacketType.MapDescription, typeof(MapDescriptionPacketWriter) },
                { OutgoingGamePacketType.MapSliceNorth, typeof(MapPartialDescriptionPacketWriter) },
                { OutgoingGamePacketType.MapSliceEast, typeof(MapPartialDescriptionPacketWriter) },
                { OutgoingGamePacketType.MapSliceSouth, typeof(MapPartialDescriptionPacketWriter) },
                { OutgoingGamePacketType.MapSliceWest, typeof(MapPartialDescriptionPacketWriter) },
                { OutgoingGamePacketType.PlayerConditions, typeof(PlayerConditionsPacketWriter) },
                { OutgoingGamePacketType.InventoryEmpty, typeof(PlayerInventoryClearSlotPacketWriter) },
                { OutgoingGamePacketType.InventoryItem, typeof(PlayerInventorySetSlotPacketWriter) },
                { OutgoingGamePacketType.PlayerSkills, typeof(PlayerSkillsPacketWriter) },
                { OutgoingGamePacketType.PlayerStats, typeof(PlayerStatsPacketWriter) },
                { OutgoingGamePacketType.ProjectileEffect, typeof(ProjectilePacketWriter) },
                { OutgoingGamePacketType.RemoveThing, typeof(RemoveAtPositionPacketWriter) },
                { OutgoingGamePacketType.Square, typeof(SquarePacketWriter) },
                { OutgoingGamePacketType.PlayerLogin, typeof(PlayerLoginPacketWriter) },
                { OutgoingGamePacketType.TextMessage, typeof(TextMessagePacketWriter) },
                { OutgoingGamePacketType.TileUpdate, typeof(TileUpdatePacketWriter) },
                { OutgoingGamePacketType.CancelAttack, typeof(PlayerCancelAttackPacketWriter) },
                { OutgoingGamePacketType.CancelWalk, typeof(PlayerCancelWalkPacketWriter) },
                { OutgoingGamePacketType.WorldLight, typeof(WorldLightPacketWriter) },
            };

            foreach (var (packetType, type) in packetWritersToAdd)
            {
                services.TryAddSingleton(type);
            }

            services.AddSingleton(s =>
            {
                var protocol = new GameProtocol_v772(s.GetRequiredService<ILogger>());

                foreach (var (packetType, type) in packetReadersToAdd)
                {
                    protocol.RegisterPacketReader((byte)packetType, s.GetRequiredService(type) as IPacketReader);
                }

                foreach (var (packetType, type) in packetWritersToAdd)
                {
                    protocol.RegisterPacketWriter((byte)packetType, s.GetRequiredService(type) as IPacketWriter);
                }

                return protocol;
            });

            services.TryAddSingleton<IProtocolTileDescriptor, TileDescriptor_v772>();

            services.TryAddSingleton<IPredefinedItemSet, PredefinedItemSet_v772>();

            services.TryAddSingleton<ClientConnectionFactory<GameProtocol_v772>>();
            services.TryAddSingleton<ISocketConnectionFactory>(s => s.GetService<ClientConnectionFactory<GameProtocol_v772>>());

            services.TryAddSingleton<GameListener<ClientConnectionFactory<GameProtocol_v772>>>();
            services.AddSingleton<IListener>(s => s.GetService<GameListener<ClientConnectionFactory<GameProtocol_v772>>>());

            // Since they are derived from IHostedService should be also registered as such.
            services.AddHostedService(s => s.GetService<GameListener<ClientConnectionFactory<GameProtocol_v772>>>());
        }

        /// <summary>
        /// Adds all the gateway server components related to protocol 7.72 contained in this library to the services collection.
        /// It also configures any <see cref="IOptions{T}"/> required by any such components.
        /// </summary>
        /// <param name="services">The services collection.</param>
        /// <param name="configuration">The configuration loaded.</param>
        public static void AddProtocol772GatewayServerComponents(this IServiceCollection services, IConfiguration configuration)
        {
            configuration.ThrowIfNull(nameof(configuration));

            // Configure the options required by the services we're about to add.
            services.Configure<GatewayListenerOptions>(configuration.GetSection(nameof(GatewayListenerOptions)));

            // Add all handlers
            services.TryAddSingleton<GatewayLogInPacketReader>();

            var packetWritersToAdd = new Dictionary<OutgoingGatewayPacketType, Type>()
            {
                { OutgoingGatewayPacketType.CharacterList, typeof(CharacterListPacketWriter) },
                { OutgoingGatewayPacketType.Disconnect, typeof(GatewayServerDisconnectPacketWriter) },
                { OutgoingGatewayPacketType.MessageOfTheDay, typeof(MessageOfTheDayPacketWriter) },
            };

            foreach (var (packetType, type) in packetWritersToAdd)
            {
                services.TryAddSingleton(type);
            }

            services.AddSingleton(s =>
            {
                var protocol = new GatewayProtocol_v772(s.GetRequiredService<ILogger>());

                protocol.RegisterPacketReader((byte)IncomingGatewayPacketType.LogInRequest, s.GetRequiredService<GatewayLogInPacketReader>());

                foreach (var (packetType, type) in packetWritersToAdd)
                {
                    protocol.RegisterPacketWriter((byte)packetType, s.GetRequiredService(type) as IPacketWriter);
                }

                return protocol;
            });

            services.TryAddSingleton<ClientConnectionFactory<GatewayProtocol_v772>>();
            services.TryAddSingleton<ISocketConnectionFactory>(s => s.GetService<ClientConnectionFactory<GatewayProtocol_v772>>());

            services.TryAddSingleton<GatewayListener<ClientConnectionFactory<GatewayProtocol_v772>>>();
            services.AddSingleton<IListener>(s => s.GetService<GatewayListener<ClientConnectionFactory<GatewayProtocol_v772>>>());

            // Since they are derived from IHostedService should be also registered as such.
            services.AddHostedService(s => s.GetService<GatewayListener<ClientConnectionFactory<GatewayProtocol_v772>>>());
        }
    }
}
