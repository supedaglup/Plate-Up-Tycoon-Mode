# PlateUp! Tycoon Mode

Tycoon Mode is a complete overhaul mod that introduces an entirely new game mode selectable from the Main Menu screen. It transforms PlateUp!'s traditional, rogue-like gameplay loop into a longform restaurant management simulator, where you must earn your stars by increasing your restaurant's Reputation, through a variety of means.

Tycoon Mode is separate from regular PlateUp!. Your normal runs and saves are never touched.

## Installation

**You need:** PlateUp! on Steam (Windows), plus these two free mods from the Steam Workshop:
- [KitchenLib](https://steamcommunity.com/sharedfiles/filedetails/?id=2898069883)
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2898033283)

1. Subscribe to KitchenLib and Harmony, start PlateUp! once so Steam downloads them, then close the game.
2. Download `PlateUpTycoon-<version>.zip` from the [Releases](../../releases) page.
3. In Steam, right-click PlateUp! → **Manage** → **Browse local files**, then open the **PlateUp** folder.
4. Open the **Mods** folder inside it (create it if it isn't there).
5. Copy the **PlateUpTycoon** folder from the zip into **Mods**. It should end up as:
   `...\PlateUp\PlateUp\Mods\PlateUpTycoon\content\PlateUpTycoon.dll`
6. Start PlateUp!. The title screen now has a **Play Tycoon** button.

**Updating:** close the game and replace the PlateUpTycoon folder with the one from the new zip.
**Uninstalling:** delete the PlateUpTycoon folder.
**Multiplayer:** every player needs the mod, on the same version.

## Getting started

1. On the title screen choose **Play Tycoon**, then pick a company slot.
2. Meet the HQ's cats: each one explains their shop the first time you visit.
3. Walk onto the loading bay to open your starter restaurant.

## What's different

- **No run end.** How busy and demanding your restaurant is follows its **star rating** (0 to 5), not the day number.
- **Real money.** The till is the company's wallet, shared by all your restaurants. Every night you pay a bill:
  utilities, upkeep, restocking, cleaning and wages for friends. Two closings in a row in debt means bankruptcy.
- **You buy everything.** Appliances at the Purchasing Desk in the restaurant (they arrive the next day), recipes,
  customer cards and discount appliances at the HQ shops. There are no free picks.
- **Reputation and stars.** Every serve earns reputation for speed, cleanliness, the cards you run and how good the
  restaurant looks; some fades every night. Reach a star's total and a food critic visits to award it. Let it slip
  and a critic can take it away. A Reputation Report after each day shows where it came from.

## New systems

- **The Bank:** loans with a repayment plan you choose (the first 3 nights are payment-free), and new restaurant sites
  to buy or lease.
- **Management Desk:** switch cards on and off, and from higher stars set prices, ingredient quality, opening and
  closing times, marketing and contracts.
- **Recipe Mastery:** serving a recipe levels it up and unlocks its add-ons.
- **Kitchen load:** a bigger menu brings fewer customers, but each one pays more.
- **Take-Out and Fast Casual:** customers order at a counter and take their food away, or sit down to eat.
- **Ambience:** furniture, wallpaper and flooring customers can see raise or lower the reputation every serve earns.
- **Health inspector:** rare surprise visits; messes and full bins they find cost reputation and a fine.
- **Garage storage:** keep appliances and cards in the HQ garage for later.

## Star rewards

| Stars | Unlocks |
|---|---|
| 1 | Upgraded appliances, Dish Pricing |
| 2 | Wallpaper and flooring, Ingredient Quality |
| 3 | Business Hours, theme cards, Marketing, Fast Casual Dining |
| 4 | Contracts |
| 5 | Five-Star Menu Prices, luxury contracts |

Lose a star and what it unlocked stops working until you earn it back.

## Reporting a bug

Open an [issue](../../issues) with a short description, a screenshot if you can, and `TycoonLog.txt`, found here
(paste the path into the File Explorer address bar):

`%USERPROFILE%\AppData\LocalLow\It's Happening\PlateUp\`

If you restarted the game since the problem, attach `TycoonLog-previous.txt` too. In multiplayer, the host's log
matters most.

## Building from source

Requires the [.NET SDK](https://dotnet.microsoft.com/download). Copy the game DLLs listed in
[lib/README.md](lib/README.md) into `lib/` (they are not included), set `PlateUpGameFolder` in
`PlateUpTycoon.csproj` if your Steam library is elsewhere, close the game and run
`dotnet build PlateUpTycoon.csproj -c Release`. The build installs the DLL into the game's Mods folder.

## Licence

[GNU General Public License v3.0](LICENSE). PlateUp! and its assets belong to their owners.
