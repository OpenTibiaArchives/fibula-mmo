﻿// -----------------------------------------------------------------
// <copyright file="CreatureMovedNotification.cs" company="2Dudes">
// Copyright (c) 2018 2Dudes. All rights reserved.
// Author: Jose L. Nunez de Caceres
// jlnunez89@gmail.com
// http://linkedin.com/in/jlnunez89
//
// Licensed under the MIT license.
// See LICENSE.txt file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------

namespace Fibula.Notifications
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using Fibula.Common.Contracts.Enumerations;
    using Fibula.Common.Contracts.Structs;
    using Fibula.Common.Utilities;
    using Fibula.Communications.Contracts.Abstractions;
    using Fibula.Communications.Contracts.Enumerations;
    using Fibula.Communications.Packets.Outgoing;
    using Fibula.Creatures.Contracts.Abstractions;
    using Fibula.Map.Contracts.Abstractions;
    using Fibula.Map.Contracts.Constants;
    using Fibula.Notifications.Arguments;
    using Fibula.Notifications.Contracts.Abstractions;

    /// <summary>
    /// Class that represents a notification for when a creature has moved.
    /// </summary>
    public class CreatureMovedNotification : Notification
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CreatureMovedNotification"/> class.
        /// </summary>
        /// <param name="findTargetPlayers">A function to determine the target players of this notification.</param>
        /// <param name="arguments">The arguments for this notification.</param>
        public CreatureMovedNotification(Func<IEnumerable<IPlayer>> findTargetPlayers, CreatureMovedNotificationArguments arguments)
            : base(findTargetPlayers)
        {
            arguments.ThrowIfNull(nameof(arguments));

            this.Arguments = arguments;
        }

        /// <summary>
        /// Gets this notification's arguments.
        /// </summary>
        public CreatureMovedNotificationArguments Arguments { get; }

        /// <summary>
        /// Finalizes the notification in preparation to it being sent.
        /// </summary>
        /// <param name="context">The context of this notification.</param>
        /// <param name="player">The player which this notification is being prepared for.</param>
        /// <returns>A collection of <see cref="IOutboundPacket"/>s, the ones to be sent.</returns>
        protected override IEnumerable<IOutboundPacket> Prepare(INotificationContext context, IPlayer player)
        {
            var packets = new List<IOutboundPacket>();
            var creature = context.CreatureFinder.FindCreatureById(this.Arguments.CreatureId);

            var allCreatureIdsToLearn = new List<uint>();
            var allCreatureIdsToForget = new List<uint>();

            if (this.Arguments.CreatureId == player.Id)
            {
                if (this.Arguments.WasTeleport)
                {
                    if (this.Arguments.OldStackPosition <= MapConstants.MaximumNumberOfThingsToDescribePerTile)
                    {
                        // Since this was described to the client before, we send a packet that lets them know the thing must be removed from that Tile's stack.
                        packets.Add(new RemoveAtLocationPacket(this.Arguments.OldLocation, this.Arguments.OldStackPosition));
                    }

                    // Then send the entire description at the new location.
                    var (descriptionMetadata, descriptionBytes) = context.MapDescriptor.DescribeAt(player, this.Arguments.NewLocation);

                    packets.Add(new MapDescriptionPacket(this.Arguments.NewLocation, descriptionBytes));

                    return packets;
                }

                if (this.Arguments.OldLocation.Z == 7 && this.Arguments.NewLocation.Z > 7)
                {
                    if (this.Arguments.OldStackPosition <= MapConstants.MaximumNumberOfThingsToDescribePerTile)
                    {
                        packets.Add(new RemoveAtLocationPacket(this.Arguments.OldLocation, this.Arguments.OldStackPosition));
                    }
                }
                else
                {
                    packets.Add(new CreatureMovedPacket(this.Arguments.OldLocation, this.Arguments.OldStackPosition, this.Arguments.NewLocation));
                }

                // floor change down
                if (this.Arguments.NewLocation.Z > this.Arguments.OldLocation.Z)
                {
                    var windowStartLocation = new Location()
                    {
                        X = this.Arguments.OldLocation.X - ((MapConstants.DefaultWindowSizeX / 2) - 1), // -8
                        Y = this.Arguments.OldLocation.Y - ((MapConstants.DefaultWindowSizeY / 2) - 1), // -6
                        Z = this.Arguments.NewLocation.Z,
                    };

                    (IDictionary<string, object> Metadata, ReadOnlySequence<byte> Data) description;

                    // going from surface to underground
                    if (this.Arguments.NewLocation.Z == 8)
                    {
                        // Client already has the two floors above (6 and 7), so it needs 8 (new current), and 2 below.
                        description = context.MapDescriptor.DescribeWindow(
                            player,
                            (ushort)windowStartLocation.X,
                            (ushort)windowStartLocation.Y,
                            this.Arguments.NewLocation.Z,
                            (sbyte)(this.Arguments.NewLocation.Z + 2),
                            MapConstants.DefaultWindowSizeX,
                            MapConstants.DefaultWindowSizeY,
                            -1);
                    }

                    // going further down underground; watch for world's deepest floor (hardcoded for now).
                    else if (this.Arguments.NewLocation.Z > 8 && this.Arguments.NewLocation.Z < 14)
                    {
                        // Client already has all floors needed except the new deepest floor, so it needs the 2th floor below the current.
                        description = context.MapDescriptor.DescribeWindow(
                            player,
                            (ushort)windowStartLocation.X,
                            (ushort)windowStartLocation.Y,
                            (sbyte)(this.Arguments.NewLocation.Z + 2),
                            (sbyte)(this.Arguments.NewLocation.Z + 2),
                            MapConstants.DefaultWindowSizeX,
                            MapConstants.DefaultWindowSizeY,
                            -3);
                    }

                    // going down but still above surface, so client has all floors.
                    else
                    {
                        description = (new Dictionary<string, object>(), ReadOnlySequence<byte>.Empty);
                    }

                    packets.Add(new MapPartialDescriptionPacket(OutgoingGamePacketType.FloorChangeDown, description.Data));

                    // moving down a floor makes us out of sync, include east and south
                    var (eastDescriptionMetadata, eastDescriptionBytes) = this.EastSliceDescription(
                            context,
                            player,
                            this.Arguments.OldLocation.X - this.Arguments.NewLocation.X,
                            this.Arguments.OldLocation.Y - this.Arguments.NewLocation.Y + this.Arguments.OldLocation.Z - this.Arguments.NewLocation.Z);

                    if (eastDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToLearnMetadataKeyName, out object eastCreatureIdsToLearnBoxed) &&
                        eastDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToForgetMetadataKeyName, out object eastCreatureIdsToForgetBoxed) &&
                        eastCreatureIdsToLearnBoxed is IEnumerable<uint> eastCreatureIdsToLearn && eastCreatureIdsToForgetBoxed is IEnumerable<uint> eastCreatureIdsToForget)
                    {
                        allCreatureIdsToLearn.AddRange(eastCreatureIdsToLearn);
                        allCreatureIdsToForget.AddRange(eastCreatureIdsToForget);
                    }

                    packets.Add(new MapPartialDescriptionPacket(OutgoingGamePacketType.MapSliceEast, eastDescriptionBytes));

                    var (southDescriptionMetadata, southDescriptionBytes) = this.SouthSliceDescription(context, player, this.Arguments.OldLocation.Y - this.Arguments.NewLocation.Y);

                    if (southDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToLearnMetadataKeyName, out object southCreatureIdsToLearnBoxed) &&
                        southDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToForgetMetadataKeyName, out object southCreatureIdsToForgetBoxed) &&
                        southCreatureIdsToLearnBoxed is IEnumerable<uint> southCreatureIdsToLearn && southCreatureIdsToForgetBoxed is IEnumerable<uint> southCreatureIdsToForget)
                    {
                        allCreatureIdsToLearn.AddRange(southCreatureIdsToLearn);
                        allCreatureIdsToForget.AddRange(southCreatureIdsToForget);
                    }

                    packets.Add(new MapPartialDescriptionPacket(OutgoingGamePacketType.MapSliceSouth, southDescriptionBytes));
                }

                // floor change up
                else if (this.Arguments.NewLocation.Z < this.Arguments.OldLocation.Z)
                {
                    var windowStartLocation = new Location()
                    {
                        X = this.Arguments.OldLocation.X - ((MapConstants.DefaultWindowSizeX / 2) - 1), // -8
                        Y = this.Arguments.OldLocation.Y - ((MapConstants.DefaultWindowSizeY / 2) - 1), // -6
                        Z = this.Arguments.NewLocation.Z,
                    };

                    (IDictionary<string, object> Metadata, ReadOnlySequence<byte> Data) description;

                    // going to surface
                    if (this.Arguments.NewLocation.Z == 7)
                    {
                        // Client already has the first two above-the-ground floors (6 and 7), so it needs 0-5 above.
                        description = context.MapDescriptor.DescribeWindow(
                            player,
                            (ushort)windowStartLocation.X,
                            (ushort)windowStartLocation.Y,
                            5,
                            0,
                            MapConstants.DefaultWindowSizeX,
                            MapConstants.DefaultWindowSizeY,
                            3);
                    }

                    // going up but still underground
                    else if (this.Arguments.NewLocation.Z > 7)
                    {
                        // Client already has all floors needed except the new highest floor, so it needs the 2th floor above the current.
                        description = context.MapDescriptor.DescribeWindow(
                            player,
                            (ushort)windowStartLocation.X,
                            (ushort)windowStartLocation.Y,
                            (sbyte)(this.Arguments.NewLocation.Z - 2),
                            (sbyte)(this.Arguments.NewLocation.Z - 2),
                            MapConstants.DefaultWindowSizeX,
                            MapConstants.DefaultWindowSizeY,
                            3);
                    }

                    // already above surface, so client has all floors.
                    else
                    {
                        description = (new Dictionary<string, object>(), ReadOnlySequence<byte>.Empty);
                    }

                    packets.Add(new MapPartialDescriptionPacket(OutgoingGamePacketType.FloorChangeUp, description.Data));

                    // moving up a floor up makes us out of sync, include west and north
                    var (westDescriptionMetadata, westDescriptionBytes) = this.WestSliceDescription(
                            context,
                            player,
                            this.Arguments.OldLocation.X - this.Arguments.NewLocation.X,
                            this.Arguments.OldLocation.Y - this.Arguments.NewLocation.Y + this.Arguments.OldLocation.Z - this.Arguments.NewLocation.Z);

                    if (westDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToLearnMetadataKeyName, out object westCreatureIdsToLearnBoxed) &&
                        westDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToForgetMetadataKeyName, out object westCreatureIdsToForgetBoxed) &&
                        westCreatureIdsToLearnBoxed is IEnumerable<uint> westCreatureIdsToLearn && westCreatureIdsToForgetBoxed is IEnumerable<uint> westCreatureIdsToForget)
                    {
                        allCreatureIdsToLearn.AddRange(westCreatureIdsToLearn);
                        allCreatureIdsToForget.AddRange(westCreatureIdsToForget);
                    }

                    packets.Add(new MapPartialDescriptionPacket(OutgoingGamePacketType.MapSliceWest, westDescriptionBytes));

                    var (northDescriptionMetadata, northDescriptionBytes) = this.NorthSliceDescription(context, player, this.Arguments.OldLocation.Y - this.Arguments.NewLocation.Y);

                    if (northDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToLearnMetadataKeyName, out object northCreatureIdsToLearnBoxed) &&
                        northDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToForgetMetadataKeyName, out object northCreatureIdsToForgetBoxed) &&
                        northCreatureIdsToLearnBoxed is IEnumerable<uint> northCreatureIdsToLearn && northCreatureIdsToForgetBoxed is IEnumerable<uint> northCreatureIdsToForget)
                    {
                        allCreatureIdsToLearn.AddRange(northCreatureIdsToLearn);
                        allCreatureIdsToForget.AddRange(northCreatureIdsToForget);
                    }

                    packets.Add(new MapPartialDescriptionPacket(OutgoingGamePacketType.MapSliceNorth, northDescriptionBytes));
                }

                if (this.Arguments.OldLocation.Y > this.Arguments.NewLocation.Y)
                {
                    // Creature is moving north, so we need to send the additional north bytes.
                    var (northDescriptionMetadata, northDescriptionBytes) = this.NorthSliceDescription(context, player);

                    if (northDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToLearnMetadataKeyName, out object northCreatureIdsToLearnBoxed) &&
                        northDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToForgetMetadataKeyName, out object northCreatureIdsToForgetBoxed) &&
                        northCreatureIdsToLearnBoxed is IEnumerable<uint> northCreatureIdsToLearn && northCreatureIdsToForgetBoxed is IEnumerable<uint> northCreatureIdsToForget)
                    {
                        allCreatureIdsToLearn.AddRange(northCreatureIdsToLearn);
                        allCreatureIdsToForget.AddRange(northCreatureIdsToForget);
                    }

                    packets.Add(new MapPartialDescriptionPacket(OutgoingGamePacketType.MapSliceNorth, northDescriptionBytes));
                }
                else if (this.Arguments.OldLocation.Y < this.Arguments.NewLocation.Y)
                {
                    // Creature is moving south, so we need to send the additional south bytes.
                    var (southDescriptionMetadata, southDescriptionBytes) = this.SouthSliceDescription(context, player);

                    if (southDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToLearnMetadataKeyName, out object southCreatureIdsToLearnBoxed) &&
                        southDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToForgetMetadataKeyName, out object southCreatureIdsToForgetBoxed) &&
                        southCreatureIdsToLearnBoxed is IEnumerable<uint> southCreatureIdsToLearn && southCreatureIdsToForgetBoxed is IEnumerable<uint> southCreatureIdsToForget)
                    {
                        allCreatureIdsToLearn.AddRange(southCreatureIdsToLearn);
                        allCreatureIdsToForget.AddRange(southCreatureIdsToForget);
                    }

                    packets.Add(new MapPartialDescriptionPacket(OutgoingGamePacketType.MapSliceSouth, southDescriptionBytes));
                }

                if (this.Arguments.OldLocation.X < this.Arguments.NewLocation.X)
                {
                    // Creature is moving east, so we need to send the additional east bytes.
                    var (eastDescriptionMetadata, eastDescriptionBytes) = this.EastSliceDescription(context, player);

                    if (eastDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToLearnMetadataKeyName, out object eastCreatureIdsToLearnBoxed) &&
                        eastDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToForgetMetadataKeyName, out object eastCreatureIdsToForgetBoxed) &&
                        eastCreatureIdsToLearnBoxed is IEnumerable<uint> eastCreatureIdsToLearn && eastCreatureIdsToForgetBoxed is IEnumerable<uint> eastCreatureIdsToForget)
                    {
                        allCreatureIdsToLearn.AddRange(eastCreatureIdsToLearn);
                        allCreatureIdsToForget.AddRange(eastCreatureIdsToForget);
                    }

                    packets.Add(new MapPartialDescriptionPacket(OutgoingGamePacketType.MapSliceEast, eastDescriptionBytes));
                }
                else if (this.Arguments.OldLocation.X > this.Arguments.NewLocation.X)
                {
                    // Creature is moving west, so we need to send the additional west bytes.
                    var (westDescriptionMetadata, westDescriptionBytes) = this.WestSliceDescription(context, player);

                    if (westDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToLearnMetadataKeyName, out object westCreatureIdsToLearnBoxed) &&
                        westDescriptionMetadata.TryGetValue(IMapDescriptor.CreatureIdsToForgetMetadataKeyName, out object westCreatureIdsToForgetBoxed) &&
                        westCreatureIdsToLearnBoxed is IEnumerable<uint> westCreatureIdsToLearn && westCreatureIdsToForgetBoxed is IEnumerable<uint> westCreatureIdsToForget)
                    {
                        allCreatureIdsToLearn.AddRange(westCreatureIdsToLearn);
                        allCreatureIdsToForget.AddRange(westCreatureIdsToForget);
                    }

                    packets.Add(new MapPartialDescriptionPacket(OutgoingGamePacketType.MapSliceWest, westDescriptionBytes));
                }
            }
            else if (player.CanSee(this.Arguments.OldLocation) && player.CanSee(this.Arguments.NewLocation))
            {
                if (player.CanSee(creature))
                {
                    if (this.Arguments.WasTeleport || (this.Arguments.OldLocation.Z == 7 && this.Arguments.NewLocation.Z > 7) || this.Arguments.OldStackPosition > 9)
                    {
                        if (this.Arguments.OldStackPosition <= MapConstants.MaximumNumberOfThingsToDescribePerTile)
                        {
                            packets.Add(new RemoveAtLocationPacket(this.Arguments.OldLocation, this.Arguments.OldStackPosition));
                        }

                        var creatureIsKnown = player.Client.KnowsCreatureWithId(this.Arguments.CreatureId);
                        var creatureIdToForget = player.Client.ChooseCreatureToRemoveFromKnownSet();

                        if (!creatureIsKnown)
                        {
                            allCreatureIdsToLearn.Add(this.Arguments.CreatureId);
                        }

                        if (creatureIdToForget > uint.MinValue)
                        {
                            allCreatureIdsToForget.Add(creatureIdToForget);
                        }

                        packets.Add(new AddCreaturePacket(creature, creatureIsKnown, creatureIdToForget));
                    }
                    else
                    {
                        packets.Add(new CreatureMovedPacket(this.Arguments.OldLocation, this.Arguments.OldStackPosition, this.Arguments.NewLocation));
                    }
                }
            }
            else if (player.CanSee(this.Arguments.OldLocation) && !player.CanSee(creature))
            {
                if (this.Arguments.OldStackPosition <= MapConstants.MaximumNumberOfThingsToDescribePerTile)
                {
                    packets.Add(new RemoveAtLocationPacket(this.Arguments.OldLocation, this.Arguments.OldStackPosition));
                }
            }
            else if (player.CanSee(this.Arguments.NewLocation) && player.CanSee(creature))
            {
                if (this.Arguments.NewStackPosition <= MapConstants.MaximumNumberOfThingsToDescribePerTile)
                {
                    var creatureIsKnown = player.Client.KnowsCreatureWithId(this.Arguments.CreatureId);
                    var creatureIdToForget = player.Client.ChooseCreatureToRemoveFromKnownSet();

                    if (!creatureIsKnown)
                    {
                        allCreatureIdsToLearn.Add(this.Arguments.CreatureId);
                    }

                    if (creatureIdToForget > uint.MinValue)
                    {
                        allCreatureIdsToForget.Add(creatureIdToForget);
                    }

                    packets.Add(new AddCreaturePacket(creature, creatureIsKnown, creatureIdToForget));
                }
            }

            if (this.Arguments.WasTeleport)
            {
                packets.Add(new MagicEffectPacket(this.Arguments.NewLocation, AnimatedEffect.BubbleBlue));
            }

            this.Sent += (client) =>
            {
                foreach (var creatureId in allCreatureIdsToLearn)
                {
                    client.AddKnownCreature(creatureId);
                }

                foreach (var creatureId in allCreatureIdsToForget)
                {
                    client.RemoveKnownCreature(creatureId);
                }
            };

            return packets;
        }

        private (IDictionary<string, object> descriptionMetadata, ReadOnlySequence<byte> descriptionData) NorthSliceDescription(INotificationContext notificationContext, IPlayer player, int floorChangeOffset = 0)
        {
            // A = old location, B = new location.
            //
            //       |------ MapConstants.DefaultWindowSizeX = 18 ------|
            //                           as seen by A
            //       x  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .     ---
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  B  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  A  .  .  .  .  .  .  .  .  .      | MapConstants.DefaultWindowSizeY = 14
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      | as seen by A
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .     ---
            //
            // x = target start of window (~) to refresh.
            var windowStartLocation = new Location()
            {
                // -8
                X = this.Arguments.OldLocation.X - ((MapConstants.DefaultWindowSizeX / 2) - 1),

                // -6
                Y = this.Arguments.NewLocation.Y - ((MapConstants.DefaultWindowSizeY / 2) - 1 - floorChangeOffset),

                Z = this.Arguments.NewLocation.Z,
            };

            return notificationContext.MapDescriptor.DescribeWindow(
                    player,
                    (ushort)windowStartLocation.X,
                    (ushort)windowStartLocation.Y,
                    (sbyte)(this.Arguments.NewLocation.IsUnderground ? Math.Max(0, this.Arguments.NewLocation.Z - 2) : 7),
                    (sbyte)(this.Arguments.NewLocation.IsUnderground ? Math.Min(15, this.Arguments.NewLocation.Z + 2) : 0),
                    MapConstants.DefaultWindowSizeX,
                    1);
        }

        private (IDictionary<string, object> descriptionMetadata, ReadOnlySequence<byte> descriptionData) SouthSliceDescription(INotificationContext notificationContext, IPlayer player, int floorChangeOffset = 0)
        {
            // A = old location, B = new location
            //
            //       |------ MapConstants.DefaultWindowSizeX = 18 ------|
            //                           as seen by A
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .     ---
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  A  .  .  .  .  .  .  .  .  .      | MapConstants.DefaultWindowSizeY = 14
            //       .  .  .  .  .  .  .  .  B  .  .  .  .  .  .  .  .  .      | as seen by A
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .      |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .     ---
            //       x  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~  ~
            //
            // x = target start of window (~) to refresh.
            var windowStartLocation = new Location()
            {
                // -8
                X = this.Arguments.OldLocation.X - ((MapConstants.DefaultWindowSizeX / 2) - 1),

                // +7
                Y = this.Arguments.NewLocation.Y + (MapConstants.DefaultWindowSizeY / 2) + floorChangeOffset,

                Z = this.Arguments.NewLocation.Z,
            };

            return notificationContext.MapDescriptor.DescribeWindow(
                    player,
                    (ushort)windowStartLocation.X,
                    (ushort)windowStartLocation.Y,
                    (sbyte)(this.Arguments.NewLocation.IsUnderground ? Math.Max(0, this.Arguments.NewLocation.Z - 2) : 7),
                    (sbyte)(this.Arguments.NewLocation.IsUnderground ? Math.Min(15, this.Arguments.NewLocation.Z + 2) : 0),
                    MapConstants.DefaultWindowSizeX,
                    1);
        }

        private (IDictionary<string, object> descriptionMetadata, ReadOnlySequence<byte> descriptionData) EastSliceDescription(INotificationContext notificationContext, IPlayer player, int floorChangeOffsetX = 0, int floorChangeOffsetY = 0)
        {
            // A = old location, B = new location
            //
            //       |------ MapConstants.DefaultWindowSizeX = 18 ------|
            //                           as seen by A
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  x  ---
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  ~   |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  ~   |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  ~   |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  ~   |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  ~   |
            //       .  .  .  .  .  .  .  .  A  B  .  .  .  .  .  .  .  .  ~   | MapConstants.DefaultWindowSizeY = 14
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  ~   | as seen by A
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  ~   |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  ~   |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  ~   |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  ~   |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  ~   |
            //       .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  ~  ---
            //
            // x = target start of window (~) to refresh.
            var windowStartLocation = new Location()
            {
                // +9
                X = this.Arguments.NewLocation.X + (MapConstants.DefaultWindowSizeX / 2) + floorChangeOffsetX,

                // -6
                Y = this.Arguments.NewLocation.Y - ((MapConstants.DefaultWindowSizeY / 2) - 1) + floorChangeOffsetY,

                Z = this.Arguments.NewLocation.Z,
            };

            return notificationContext.MapDescriptor.DescribeWindow(
                    player,
                    (ushort)windowStartLocation.X,
                    (ushort)windowStartLocation.Y,
                    (sbyte)(this.Arguments.NewLocation.IsUnderground ? Math.Max(0, this.Arguments.NewLocation.Z - 2) : 7),
                    (sbyte)(this.Arguments.NewLocation.IsUnderground ? Math.Min(15, this.Arguments.NewLocation.Z + 2) : 0),
                    1,
                    MapConstants.DefaultWindowSizeY);
        }

        private (IDictionary<string, object> descriptionMetadata, ReadOnlySequence<byte> descriptionData) WestSliceDescription(INotificationContext notificationContext, IPlayer player, int floorChangeOffsetX = 0, int floorChangeOffsetY = 0)
        {
            // A = old location, B = new location
            //
            //          |------ MapConstants.DefaultWindowSizeX = 18 ------|
            //                           as seen by A
            //       x  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  ---
            //       ~  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .   |
            //       ~  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .   |
            //       ~  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .   |
            //       ~  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .   |
            //       ~  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .   |
            //       ~  .  .  .  .  .  .  .  B  A  .  .  .  .  .  .  .  .  .   | MapConstants.DefaultWindowSizeY = 14
            //       ~  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .   | as seen by A
            //       ~  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .   |
            //       ~  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .   |
            //       ~  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .   |
            //       ~  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .   |
            //       ~  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .   |
            //       ~  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  ---
            //
            // x = target start of window (~) to refresh.
            var windowStartLocation = new Location()
            {
                // -8
                X = this.Arguments.NewLocation.X - ((MapConstants.DefaultWindowSizeX / 2) - 1) + floorChangeOffsetX,

                // -6
                Y = this.Arguments.NewLocation.Y - ((MapConstants.DefaultWindowSizeY / 2) - 1) + floorChangeOffsetY,

                Z = this.Arguments.NewLocation.Z,
            };

            return notificationContext.MapDescriptor.DescribeWindow(
                    player,
                    (ushort)windowStartLocation.X,
                    (ushort)windowStartLocation.Y,
                    (sbyte)(this.Arguments.NewLocation.IsUnderground ? Math.Max(0, this.Arguments.NewLocation.Z - 2) : 7),
                    (sbyte)(this.Arguments.NewLocation.IsUnderground ? Math.Min(15, this.Arguments.NewLocation.Z + 2) : 0),
                    1,
                    MapConstants.DefaultWindowSizeY);
        }
    }
}