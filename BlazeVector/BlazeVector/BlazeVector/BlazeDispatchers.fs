﻿namespace BlazeVector
open System
open System.Collections
open OpenTK
open Microsoft.Xna
open FarseerPhysics
open FarseerPhysics.Dynamics
open Prime
open Nu

[<AutoOpen>]
module BlazeDispatchersModule =

    type Entity with

        (* bullet xfields *)
        [<XField>] member this.BirthTime with get () = this?BirthTime () : uint64
        member this.SetBirthTime (value : uint64) : Entity = this?BirthTime <- value

    type BlazeBulletDispatcher () =
        inherit Entity2dWithSimplePhysicsAndRenderingDispatcher ()

        let launch (bullet : Entity) world =
            let applyLinearImpulseMessage = ApplyLinearImpulseMessage { PhysicsId = bullet.PhysicsId; LinearImpulse = Vector2 (400.0f, 0.0f) }
            { world with PhysicsMessages = applyLinearImpulseMessage :: world.PhysicsMessages }

        let tickHandler _ _ address message world =
            let bullet = get world <| World.worldEntity address
            if bullet.BirthTime + 30UL > world.Ticks then (message, true, world)
            else
                let world = World.removeEntity address world
                (message, true, world)

        override this.MakeBodyShape (bullet : Entity) =
            CircleShape { Radius = bullet.Size.X * 0.5f; Center = Vector2.Zero }

        override this.GetImageSpriteAssetName () =
            "Image7"

        override dispatcher.Init (bullet, dispatcherContainer) =
            let bullet = base.Init (bullet, dispatcherContainer)
            bullet
                .SetLinearDamping(0.0f)
                .SetGravityScale(0.0f)
                .SetIsBullet(true)
                .SetIsSensor(true)
                .SetBirthTime(0UL)
                .SetSize(Vector2 (12.0f, 12.0f))

        override dispatcher.Register (bullet, address, world) =
            let world = base.Register (bullet, address, world)
            let world = World.subscribe NuConstants.TickEvent address (CustomSub tickHandler) world
            let bullet = bullet.SetBirthTime world.Ticks
            let world = set bullet world <| World.worldEntity address
            launch bullet world

        override dispatcher.Unregister (bullet, address, world) =
            let world = base.Unregister (bullet, address, world)
            World.unsubscribe NuConstants.TickEvent address world

    type BlazePlayerDispatcher () =
        inherit CharacterDispatcher ()

        let createBullet (player : Entity) playerAddress world =
            let bullet = Entity.makeDefault typeof<BlazeBulletDispatcher>.Name None world
            let bullet =
                bullet
                    .SetPosition(player.Position + player.Size * 0.75f)
                    .SetDepth(player.Depth + 1.0f)
            let bulletAddress = List.allButLast playerAddress @ [bullet.Name]
            World.addEntity bulletAddress bullet world

        let spawnBulletHandler _ _ address message world =
            if not world.Interactive then (message, true, world)
            else
                let player = get world <| World.worldEntity address
                if world.Ticks % 5UL <> 0UL then (message, true, world)
                else
                    let world = createBullet player address world
                    (message, true, world)

        let movePlayerHandler _ _ address message world =
            if not world.Interactive then (message, true, world)
            else
                let player = get world <| World.worldEntity address
                let optGroundTangent = Physics.getOptGroundContactTangent player.PhysicsId world.Integrator
                let force =
                    match optGroundTangent with
                    | None -> Vector2 (1.0f, -2.5f) * 3000.0f
                    | Some groundTangent -> Vector2.Multiply (groundTangent, Vector2 (3000.0f, if groundTangent.Y > 0.0f then 7000.0f else 0.0f))
                let applyForceMessage = ApplyForceMessage { PhysicsId = player.PhysicsId; Force = force }
                let world = { world with PhysicsMessages = applyForceMessage :: world.PhysicsMessages }
                (message, true, world)

        let jumpPlayerHandler _ _ address message world =
            if not world.Interactive then (message, true, world)
            else
                let player = get world <| World.worldEntity address
                if not <| Physics.isBodyOnGround player.PhysicsId world.Integrator
                then (message, true, world)
                else
                    let applyLinearImpulseMessage = ApplyLinearImpulseMessage { PhysicsId = player.PhysicsId; LinearImpulse = Vector2 (0.0f, 7500.0f) }
                    let world = { world with PhysicsMessages = applyLinearImpulseMessage :: world.PhysicsMessages }
                    (message, true, world)

        override dispatcher.Register (player, address, world) =
            let world = base.Register (player, address, world)
            world |>
                World.subscribe NuConstants.TickEvent address -<| CustomSub spawnBulletHandler |>
                World.subscribe NuConstants.TickEvent address -<| CustomSub movePlayerHandler |>
                World.subscribe NuConstants.DownMouseRightEvent address -<| CustomSub jumpPlayerHandler

        override dispatcher.Unregister (player, address, world) =
            let world = base.Unregister (player, address, world)
            world |>
                World.unsubscribe NuConstants.TickEvent address |>
                World.unsubscribe NuConstants.TickEvent address |>
                World.unsubscribe NuConstants.DownMouseRightEvent address

    type BlazeEnemyDispatcher () =
        inherit CharacterDispatcher ()

        let moveEnemyHandler _ _ address message world =
            if not world.Interactive then (message, true, world)
            else
                let enemy = get world <| World.worldEntity address
                let optGroundTangent = Physics.getOptGroundContactTangent enemy.PhysicsId world.Integrator
                let force =
                    match optGroundTangent with
                    | None -> Vector2 (-1.0f, -2.5f) * 800.0f
                    | Some groundTangent -> Vector2.Multiply (groundTangent, Vector2 (-800.0f, if groundTangent.Y > 0.0f then 7000.0f else 0.0f))
                let applyForceMessage = ApplyForceMessage { PhysicsId = enemy.PhysicsId; Force = force }
                let world = { world with PhysicsMessages = applyForceMessage :: world.PhysicsMessages }
                (message, true, world)

        override dispatcher.Register (enemy, address, world) =
            let world = base.Register (enemy, address, world)
            World.subscribe NuConstants.TickEvent address (CustomSub moveEnemyHandler) world

        override dispatcher.Unregister (enemy, address, world) =
            let world = base.Unregister (enemy, address, world)
            World.unsubscribe NuConstants.TickEvent address world

    /// TODO document.
    type BlazeStageGroupDispatcher () =
        inherit GroupDispatcher ()

        let getPlayer groupAddress world =
            let playerAddress = groupAddress @ [BlazeConstants.StagePlayerName]
            get world <| World.worldEntity playerAddress

        let adjustCamera groupAddress world =
            let player = getPlayer groupAddress world
            let eyeCenter = Vector2 (player.Position.X + player.Size.X * 0.5f, world.Camera.EyeCenter.Y)
            { world with Camera = { world.Camera with EyeCenter = eyeCenter }}

        let adjustCameraHandler _ _ groupAddress message world =
            (message, true, adjustCamera groupAddress world)

        override dispatcher.Register (group, address, entities, world) =
            let world = base.Register (group, address, entities, world)
            let world = World.subscribe NuConstants.TickEvent address (CustomSub adjustCameraHandler) world
            adjustCamera address world
            
        override dispatcher.Unregister (group, address, world) =
            let world = base.Unregister (group, address, world)
            World.unsubscribe NuConstants.TickEvent address world

    type BlazeStageScreenDispatcher () =
        inherit ScreenDispatcher ()

        let shiftEntities xShift entities world =
            List.map
                (fun (entity : Entity) ->
                    if Xtension.derivesFrom typeof<Entity2dDispatcher> entity.Xtension world then entity
                    else entity.SetPosition <| entity.Position + Vector2 (xShift, 0.0f))
                entities

        let makeSectionFromFile fileName sectionName xShift world =
            let (sectionGroup, sectionEntities) = World.loadGroupFromFile fileName world
            let sectionEntities = shiftEntities xShift sectionEntities world
            (sectionName, sectionGroup, sectionEntities)

        override dispatcher.Register (screen, address, groupDescriptors, world) =
            let stagePlay = World.loadGroupFromFile BlazeConstants.StagePlayFileName world
            let stagePlayDescriptor = Triple.prepend BlazeConstants.StagePlayName stagePlay
            let sectionDescriptor0 = makeSectionFromFile BlazeConstants.Section0FileName BlazeConstants.Section0Name 0.0f world
            let sectionDescriptor1 = makeSectionFromFile BlazeConstants.Section1FileName BlazeConstants.Section1Name 2048.0f world
            let sectionDescriptor2 = makeSectionFromFile BlazeConstants.Section2FileName BlazeConstants.Section2Name 4096.0f world
            let sectionDescriptor3 = makeSectionFromFile BlazeConstants.Section3FileName BlazeConstants.Section3Name 6144.0f world
            let sectionDescriptors = [sectionDescriptor0; sectionDescriptor1; sectionDescriptor2; sectionDescriptor3]
            let groupDescriptors = stagePlayDescriptor :: sectionDescriptors @ groupDescriptors
            base.Register (screen, address, groupDescriptors, world)

        override dispatcher.Unregister (screen, address, world) =
            base.Unregister (screen, address, world)

    /// The custom type for BlazeVector's game dispatcher.
    type BlazeGameDispatcher () =
        inherit GameDispatcher ()

        override dispatcher.Register (_, world) =
            // add the BlazeVector-specific dispatchers to the world
            let dispatchers =
                Map.addMany
                    [|typeof<BlazeBulletDispatcher>.Name, BlazeBulletDispatcher () :> obj
                      typeof<BlazePlayerDispatcher>.Name, BlazePlayerDispatcher () :> obj
                      typeof<BlazeEnemyDispatcher>.Name, BlazeEnemyDispatcher () :> obj
                      typeof<BlazeStageGroupDispatcher>.Name, BlazeStageGroupDispatcher () :> obj
                      typeof<BlazeStageScreenDispatcher>.Name, BlazeStageScreenDispatcher () :> obj|]
                    world.Dispatchers
            { world with Dispatchers = dispatchers }