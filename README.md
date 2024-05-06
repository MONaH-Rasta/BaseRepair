# BaseRepair

Oxide plugin for Rust. Allows player to repair their entire base by hitting part of the building with the hammer.

## Permissions

* `baserepair.use` - allows players to use base repair
* `baserepair.nocost` - allows players with permission to not have to pay for repair
* `baserepair.noauth` - allows players with permission to not have to be authed on the TC in order to repair  
* `noescape.raid.repairblock` - blocks repair while raid blocked (Requires NoEscape plugin)  
* `noescape.combat.repairblock` - blocks repair while combat blocked (Requires NoEscape plugin)  

## Chat Commands

* `/br` - toggle base repair on and off

## Configuration

```json
{
  "Number of entities to repair per server frame": 10,
  "Default Enabled": false,
  "Allow Repairing Bases Without A Tool Cupboard": true,
  "Repair Cost Multiplier": 1.0,
  "How long after an entity is damaged before it can be repaired (Seconds)": 30.0,
  "Chat Commands": [
    "br"
  ],
  "Enable Repairs Using A Skinned Hammer": true,
  "Repair Hammer Skin ID": 2902701361
}
```

## Localization

```json
{
  "Chat": "<color=#bebebe>[<color=#de8732>Base Repair</color>] {0}</color>",
  "NoPermission": "You do not have permission to use this command",
  "RepairInProcess": "You have a current repair in progress. Please wait for that to finish before repairing again",
  "RecentlyDamaged": "We failed to repair {0} because they were recently damaged",
  "AmountRepaired": "We have repair {0} damaged items in this base. ",
  "CantAfford": "We failed to repair {0} because you were missing items to pay for it.",
  "MissingItems": "The items you were missing are:",
  "MissingItem": "{0}: {1}x",
  "Enabled": "You enabled enabled building repair. Hit the building you wish to repair with the hammer and we will do the rest for you.",
  "Disabled": "You have disabled building repair."
}
```
