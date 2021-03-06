﻿// Prime - A PRIMitivEs code library.
// Copyright (C) Bryan Edds, 2012-2015.

namespace Prime.Samples

(* This file merely contains code that exemplifies F#'s compile-time duck-typing, AKA, Structural typing. *)

type RedDuck =
    { Name : string }
    member this.Quack () = "Red"

type BlueDuck =
    { Name : string }
    member this.Quack () = "Blue"

module DuckTypingExample =

    let inline name this =
        (^a : (member Name : string) this)

    let inline quack this =
        (^a : (member Quack : unit -> string) this)

    let inline nameAndQuack this =
        let name = (^a : (member Name : string) this)
        let quack = (^a : (member Quack : unit -> string) this)
        name + " " + quack

    let howard = name { RedDuck.Name = "Howard" }
    let bob = name { BlueDuck.Name = "Bob" }
    let red = quack { RedDuck.Name = "Jim" }
    let blue = quack { BlueDuck.Name = "Fred" }
    let naq = nameAndQuack { BlueDuck.Name = "Fred" }