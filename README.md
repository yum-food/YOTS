# YOTS: yum's optimized toggle system

YOTS is a text-based system for managing VRChat toggles and sliders. It
translates a single config file into VRChat menus, parameters, animations, and
animator layers. They are added to your avatar non-destructively.

YOTS is efficient, maintainable, non-destructive, and compatible with any
existing animator.

yum!

## Installation

First, install [Modular Avatar](https://modular-avatar.nadena.dev/).

Then grab the latest release from [the releases page](https://github.com/yum-food/YOTS/releases/latest).

## Creating your first toggle

Open your text editor of choice and paste this in:

```json
{
  "api_version": "1.0",
  "toggles": [
    {
      "name": "Shirt",
      "meshToggles": ["Shirt"],
    }
  ]
}
```

Feel free to replace "Shirt" with the name of some other mesh on your avatar.

Save it to Assets/animator.json.

Drag Assets/yum\_food/YOTS.prefab anywhere on your avatar. Select it in the
hierarchy, and drag Assets/animator.json onto the "Json Config" field.

Enter play mode. Enable an emulator (I use Lyuma's av3emulator). Open your
menu. You should see a YOTS submenu. Click it, then click Shirt. Your shirt
should toggle off.

Congratulations!

A logical sequence of things to try:

1. Add more toggles.
2. Add blendshape toggles in addition to mesh toggles.
3. Declare a dependency on another toggle with `"dependencies": ["someOtherToggle"]`.
4. Install a toggle at a custom path with `"menuPath": "/my/custom/path"`.
5. Add a radial puppet with `"type": "radial"`.
6. Use `"defaultValue": 0.0` to set a toggle to off by default.

Toggle options are documented in two places:

1. The ToggleSpec definition at the top of
   [YOTSCore.cs](./Scripts/YOTSCore.cs). This is the definitive source of
   truth.
2. The [Examples](./Examples).

## Technical details

* Toggles are created as boolean parameters.
* Radial puppets (sliders) are created as float parameters.
* By default, all parameters are synced and saved. These can both be
  overridden.
* The generated VRChat parameter and animator parameters use the "name" field.
  No namespacing is applied.
* The generated animator has exactly as many layers as the maximum length of
  any dependency chain in your dependency graph.
* The generated animator's parameters and layers are simply appended to your
  input animator.

## Motivation

Animators have a two fundamental problems:

1. Layers are extremely slow. Their runtime scales with O(n^2).
2. Animations which affect the same thing cannot be in the same layer.

Any efficient animator must minimize the number of layers. Any real-world
animator requires multiple layers because some toggles will need to override
other toggles. Thus the goal is to provide this overriding capability while
still minimizing the number of layers.

In addition, whenever you add a toggle to a VRChat avatar, you need to edit at
least 4 files:

1. An animation file for the toggle (usually two)
2. The avatar menu
3. The avatar parameters
4. The animator

With YOTS, you only have to edit one file.

Finally, there are many unduly tedious tasks which you wind up performing over
the lifetime of an avatar:

1. Adding new articles of clothing. You now have to edit all your existing
   avatar-wide animations to include them.
2. Adding new toggles or sliders. You may want them to affect a large
   set of items. For example: you added a minimum brightness slider, and now
   have to animate 20 different articles of clothing.
3. Removing articles of clothing. You should remove them from your avatar-wide
   animations. (No one does this because it's a pain in the ass!)
4. Removing toggles or sliders. It's easy to accidentally orphan an animator
   layer, or a parameter somewhere.

These are all vastly easier to perform through YOTS.

## Design derivation

Consider this basic example: you have a shirt and a jacket. The shirt hides the
chest to avoid clipping. The jacket hides the shirt sleeves to hide clipping:

```
- [ToggleSpec] Shirt
  - [MeshToggle] Shirt
  - [BlendShape] Chest hidden
- [ToggleSpec] Jacket
  - [MeshToggle] Jacket
  - [BlendShape] Shirt sleeves hidden
```

A system could trivially be made to generate animations for this:

```
- [Animation] ShirtOn
  - [MeshToggle] Shirt on
  - [BlendShape] Chest hide -> 100
- [Animation] ShirtOff
  - [MeshToggle] Shirt off
  - [BlendShape] Chest hide -> 0
- [Animation] JacketOn
  - [MeshToggle] Jacket on
  - [BlendShape] Shirt sleeves hide -> 100
- [Animation] JacketOff
  - [MeshToggle] Jacket off
  - [BlendShape] Shirt sleeves hide -> 0
```

This system works perfectly as written and can be trivially implemented by
driving all 4 animations in a single layer with one DBT.
Problems arise when you have two assets that want to animate the same
blendshape. In that case, you must declare a dependency. In our example,
suppose we wanted to add an undershirt. It also wants to hide the chest:

```
- [ToggleSpec] Undershirt
  - [MeshToggle] Undershirt
  - [BlendShape] Chest hidden
```

The animations are also trivial:

```
- [Animation] UndershirtOn
  - [MeshToggle] Undershirt on
  - [BlendShape] Chest hide -> 100
- [Animation] UndershirtOff
  - [MeshToggle] Undershirt Off
  - [BlendShape] Chest hide -> 0
```

The problem is that since Undershirt{On,Off} and Shirt{On,Off} both animate the
"Chest hide" blendshape, you cannot put them into the same DBT. It's even worse
than that: if you split them into layers, such that the undershirt is evaluated
before the shirt, then if the shirt is toggled off, it will always set the
"Chest hide" blendshape to 0. With the shirt off and undershirt on, the chest
will clip through the undershirt.

To fix this, we can declare a *dependency*. In this case the order doesn't
matter, so I will just use the convention that outer layers of garments depend
on inner layers.

```
- [ToggleSpec] Undershirt
  - [MeshToggle] Undershirt
  - [BlendShape] Chest hidden
- [ToggleSpec] Shirt
  - [Dependency] Undershirt
  - [MeshToggle] Shirt
  - [BlendShape] Chest hidden
```

This situation can be detected robustly. We simply do a topological sort of all
ToggleSpec nodes according to their declared dependencies. This will give us a
set of directed acyclic graphs (a forest). We can maintain a set of attributes
affected by ToggleSpec nodes while iterating through them. **Any two nodes
which affect the same attribute must be in the same DAG and not at the same
level.** This can be surfaced to the user as a critical error. It can tell them
something like:

  Error: ToggleSpec $A and $B both animate the same property $PROPERTY. Declare
  a dependency to resolve the conflict.

We can also detect cycles in the graph (which wouldn't be possible to implement
in the animator anyway!) and report that to the user:

  Error: Cycle detected: ToggleSpecs $ALL\_AFFECTED\_TOGGLES have a cyclic
  dependency. Delete one Dependency attribute in the chain to resolve the
  conflict.

The forest of DAGs is then used to generate the animator. To generate it, you
iterate a total of n times, where n is the largest depth of any DAG in the
forest. **Each layer in the animator contains every ToggleSpec of depth k, of
any DAG in the forest.** For example, a forest with 1000 separate DAGs of
maximum depth 3 would only generate a 3-layer animator. A forest with one DAG
of depth 300 would generate a 300 layer animator. The maximum length of the DAG
characterizes the number of nodes in the animator.

There are two types of layers: the first layer, and every other layer. For the
first layer, because it's free to overwrite anything on the avatar, the DBT can
be constructed of pairs of Thing{On,Off} animations. Because of our topological
sort, we know that these nodes are all independent, so no two pairs are
animating the same thing. The config parser would have errored out by now if
that was the case.

The successive layers are comprised of ToggleSpecs which animate one or more
attributes. At least one of these attributes is already being animated. We must
split the node into two parts. One part (the independent part) consists
entirely of attributes which are not already animated. The other part consists
entirely of nodes which are being animated (the dependent part). The
independent part may be comprised of the empty set, in which case it is
discarded. If it's not empty, it's added to the first layer's DBT. The
dependent part is guaranteed to be non-empty, and is simply added to a new DBT.

### Specification language

We use JSON to represent the specification. The example above is expressed as
follows:

```json
{
  "api_version": "1.0",
  "toggles": [
    {
      "name": "Undershirt",
      "meshToggles": ["Undershirt"],
      "blendShapes": [
        {
          "path": "Body",
          "blendShape": "Chest_Hide"
        }
      ]
    },
    {
      "name": "Shirt",
      "dependencies": ["Undershirt"],
      "meshToggles": ["Shirt"],
      "blendShapes": [
        {
          "path": "Body",
          "blendShape": "Chest_Hide"
        }
      ]
    },
    {
      "name": "Jacket",
      "meshToggles": ["Jacket"],
      "blendShapes": [
        {
          "path": "Shirt",
          "blendShape": "Sleeves_Hide"
        }
      ]
    }
  ]
}
```

Given that config, we would run the described topological sort, erroring out if
there are unconnected nodes which affect the same attribute, or if there is a
cycle.

In the topological sort of the dependency graph, we have Undershirt and Jacket
running on the first layer, and Shirt running on the second layer.

Given that dependency graph, let's consider how we'd generate the animations.
The first layer's animations are trivial:

```json
{
  "animations": [
    // Shirt
    {
      "name": "Shirt_On",
      "meshToggles": [
        {
          "path": "Shirt",
          "value": 1.0
        }
      ],
      "blendShapes": [
        {
          "path": "Body",
          "blendShape": "Chest_Hide",
          "value": 1.0
        }
      ]
    },
    {
      "name": "Shirt_Off",
      "meshToggles": [
        {
          "path": "Shirt",
          "value": 0.0
        }
      ],
      "blendShapes": [
        {
          "path": "Body",
          "blendShape": "Chest_Hide",
          "value": 0.0
        }
      ]
    },
    // Jacket
    {
      "name": "Jacket_On",
      "meshToggles": [
        {
          "path": "Jacket",
          "value": 1.0
        }
      ],
      "blendShapes": [
        {
          "path": "Shirt",
          "blendShape": "Sleeves_Hide",
          "value": 1.0
        }
      ]
    },
    {
      "name": "Jacket_Off",
      "meshToggles": [
        {
          "path": "Jacket",
          "value": 0.0
        }
      ],
      "blendShapes": [
        {
          "path": "Shirt",
          "blendShape": "Sleeves_Hide",
          "value": 0.0
        }
      ]
    }
  ]
}
```

Naively, we might expect the second animations to be this:

```json
{
  "animations": [
    {
      "name": "Undershirt_On",
      "meshToggles": [
        {
          "path": "Undershirt",
          "value": 1.0
        }
      ],
      "blendShapes": [
        {
          "path": "Body",
          "blendShape": "Chest_Hide",
          "value": 0.0
        }
      ]
    },
    {
      "name": "Undershirt_Off",
      "meshToggles": [
        {
          "path": "Undershirt",
          "value": 0.0
        }
      ],
      "blendShapes": [
        {
          "path": "Body",
          "blendShape": "Chest_Hide",
          "value": 1.0
        }
      ]
    }
  ]
}
```

However, we must split the UnderShirt animations into the independent and
dependent parts:

```json
// Independent part
{
  "animations": [
    {
      "name": "Undershirt_On_Independent",
      "meshToggles": [
        {
          "path": "Undershirt",
          "value": 1.0
        }
      ],
    }
    {
      "name": "Undershirt_Off_Independent",
      "meshToggles": [
        {
          "path": "Undershirt",
          "value": 0.0
        }
      ]
    }
  ]
}
```

```json
// Dependent part
{
  "animations": [
    {
      "name": "Undershirt_On_Dependent",
      "blendShapes": [
        {
          "path": "Body",
          "blendShape": "Chest_Hide",
          "value": 1.0
        }
      ]
    }
  ]
}
```

Then we'd append the independent part to the first layer's animations. We could
then report this in a nice object:

```json
{
  "animationLayers": [
    // Layer 1
    {
      "animations": [
        { "name": "Shirt_On", ... },
        { "name": "Shirt_Off", ... },
        { "name": "Jacket_On", ... },
        { "name": "Jacket_Off", ... },
        { "name": "Undershirt_On_Independent", ... },
        { "name": "Undershirt_Off_Independent", ... }
      ]
    },
    // Layer 2
    {
      "animations": [
        { "name": "Undershirt_On_Dependent", ... },
        { "name": "Undershirt_Off_Dependent", ... }
      ]
    }
  ]
}
```

We will also need toggles for material properties. These are discussed in
Extension 2.

Our animations are complete. Our animator can trivially use the names of each
ToggleSpec as its parameters. To actually generate the first layer, we'll use
Unity's built in APIs.

// TODO document this part. For now just look at YOTSCore.cs.

We have to generate animations, configs for debug purposes, and finally an
animator file.

// TODO document this. Again just look at YOTSCore.cs.

## Extensions

### 1. Order-agnostic dependency

For ease of use, a subtype of the [Dependency] attribute called
[OrderAgnosticDependency] may be created. Its function is to allow the runtime
to create an arbitrary ordering whenever two ToggleSpecs try to affect the same
node. For the initial version, only an explicit [Dependency] is created.

### 2. GameObject material animation resolution

Animations affecting material properties necessarily animate the same property
on all materials on the same GameObject. The situation can be detected by
iterating all GameObjects on the avatar. For each skinned mesh renderer, we can
check which materials exist. **Any time a (gameobject,materials) pair is
animated, we must generate animations for (gameobject,neighbor_material) for
every neighboring material on that gameobject.** These generated animations
should be logged during generation.
