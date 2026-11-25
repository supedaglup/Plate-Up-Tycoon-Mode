# PlateUp! Tycoon Mode

Tycoon Mode turns PlateUp! into one long restaurant-management game. Instead of a fifteen-day roguelike run, you build a
company: you open a small coffee stand, pay real running costs every night, take out loans, buy every appliance and
recipe yourself, earn stars from food critics, and grow into a chain of up to five restaurants.

Everything in Tycoon Mode is separate from regular PlateUp!: start it from **Play Tycoon** on the title screen. Your
normal runs and saves are untouched.

- [Getting started](#getting-started)
- [The systems](#the-systems)
- [Formulas and balancing](#formulas-and-balancing)
- [Editing the numbers](#editing-the-numbers)
- [Building from source](#building-from-source)
- [Licence](#licence)

---

## Getting started

1. Install the mod (it needs **KitchenLib**).
2. On the title screen choose **Play Tycoon**, then pick one of three company slots.
3. At the new HQ, you will meet a couple new feline NPCs who can help you throughout your Restaurant management journey.
4. Walk onto the loading bay to open your starter restaurant.

---

## The systems

### Your company and the HQ

- **One wallet.** The restaurant's till is the company's money. Every restaurant you own shares it.
- **Up to five restaurants** per company. Each keeps its own stars, reputation, cards and appliances.
- **The HQ** has a bank, a recipe shop, a card market and a small discount appliance shop. Each owner explains their
  part the first time you visit.
- **Bankruptcy.** Ending two closings in a row with negative cash bankrupts the company.

### The Bank

- **Loans.** You can take out 1 loan at a time. You choose the repayment plan in days: longer plans mean smaller daily payments but
  more interest in total. Payments come out of the nightly bill, starting after 3 payment-free nights (loans and leases
  alike), so the money can go straight into a new restaurant.
- **Extra payments** and **plan changes** are possible at any time. A longer plan adds the extra interest on what is
  left; a shorter plan costs nothing extra.
- **Restaurant sites.** The bank sells or leases blueprints for new restaurants in different locations (City, Country,
  Alpine, Autumn). Bigger sites cost more. Event and Turbo locations are never offered, and no location hands out its Community or Turbo card.

  ### Customer Cards

Customer cards are now handled much differently, where they can be purchased at HQ for coins in order to gain more reputation per item served.

- **Crowd cards** (Loyal Locals to Tourist Hotspot): more customers every day.
- **Group cards** (Family Lunches, Office Outings, Birthday Parties, Wedding Receptions): bigger groups. Groups never
  outgrow your largest table, so a group card only earns its full reputation once a table can seat its biggest groups.
  Until then its tag in the Card Manager shows how much it earns (for example "50% seated").
- **Heat cards** appear on rare days: much harder rules, much more reputation.

### Appliance Boxes

HQ now sells kitchen appliances at a discount from the top of the garage. Both purchased appliances and any Customer or
Recipe card can be stored in the garage carts to be used later.

### The Purchasing Desk

You buy every appliance yourself at the Purchasing Desk in the restaurant. Orders placed during the day arrive the
next day. Upgraded appliances unlock at 1 star; wallpaper and flooring at 2 stars. Prices follow how much the company
has been earning, so they are cheaper while business is slow.

### The Management Desk

This new desk functions as the restaurant's control panel. In online games, only the host can use it.

- **Card Manager.** Switch any card on or off: recipes, add-ons, customer cards, themes and star rewards. Switching a
  recipe on that needs appliances you don't have yet holds the day until you buy them.
- **Ingredient Quality** (2 stars). Choose Budget, Standard, Premium or Gourmet ingredients.
- **Opening and Closing Times** (3 stars). Add up to 15 seconds before customers come in and after they stop.
- **Marketing** (3 stars). Up to three campaigns at once, each lasting a few days.
- **Contracts** (4 stars). Book a special group for the next day for a big reward, or a whole luxury VIP day at
  5 stars.

### Stars, reputation and the food critic

- Every customer you serve earns (or costs) **reputation**. Fast service, a clean floor, more cards switched on and a
  good-looking restaurant earn more. Slow service, mess and walkouts cost reputation, and some fades every night.
- When your reputation reaches the next star's total, a **food critic** visits the next day without warning. Serve them
  well and you earn the star at closing.
- If reputation falls well below your current star, a critic comes to check. Disappoint them and you lose the star.
- The **Reputation Report** after each day shows exactly where your reputation came from and where it went.

### What each star unlocks

| Stars | Reward |
|---|---|
| 1 | **Upgraded Appliances** card: the Purchasing Desk sells upgrades. **Dish Pricing** card: set your prices at the Management Desk. |
| 2 | Wallpaper and flooring. **Ingredient Quality** card (on by default, can be switched off). |
| 3 | **Business Hours** card, the four **theme** cards, **Marketing**, and the **Fast Casual Dining** card for Take-Out restaurants. |
| 4 | **Contracts**. |
| 5 | **Five-Star Menu Prices** card: every dish costs more, but queues are less patient. Lost with the fifth star. |

A restaurant that loses a star keeps its reward cards, but what they unlock stops working until the star comes back.

### Dish Pricing (1 star)

At the Management Desk you set your prices from 30% cheaper to 30% dearer, in steps of 10%. Dearer dishes earn more
coins, but each customer gives less reputation: at the top price so little that the nightly fade wins and the
restaurant slowly loses reputation, and in time a star. Cheaper dishes earn fewer coins and more reputation. The line
under the reputation tracker shows the reputation a customer is worth right now, every modifier included.

| Prices | Reputation per serve |
|---|---|
| −30% / −20% / −10% | ×1.6 / ×1.4 / ×1.2 |
| normal | ×1 |
| +10% / +20% / +30% | ×0.7 / ×0.4 / ×0.1 |

Only a serve's positive reputation is changed; slow or messy serves cost the same.

Cheaper prices wear off: each closing at cheaper prices makes the customers one day more used to them, and the extra
reputation shrinks to 70% of the day before (never below a quarter of it). Each closing at normal or dearer prices
takes one day off. The extra also shrinks with stars: full at 0 to 1 star, 85% at 2, 70% at 3, half at 4 and 5 stars
(−30% gives about ×1.3 at 4 stars).

### Take-Out and Fast Casual Dining

- Every restaurant can use the **Take-Out** card. Customers queue indoors, order at the Ordering Counter and leave with
  their food, so you need no tables. Take-Out orders pay less, and food served on a Disposable Plate earns half the
  reputation. Every Disposable Plate taken from a rack adds 1 coin (x the star cost multiplier) to that night's restocking.
- From 3 stars, **Fast Casual Dining** (with Take-Out on) lets about half of the parties take their order to a table
  and eat. They tip more, their food is drawn on the table, and friends wait for each other: in the queue behind a
  friend their patience holds, and at the table nobody leaves until their tablemates are done (40 seconds at most).
- Friends who arrive together queue one by one and are seated together; a big party spreads over several tables.
- Cards about eating time or plates (**Picky Eaters**, **Leisurely Eating**) need customers who sit down: with Take-Out
  on, they only work with Fast Casual Dining on too, and switch off otherwise. A Picky Eater's leftovers on a Disposable
  Plate are a dirty Disposable Plate that goes in the bin whole, no washing up.
- Disposable Plates count as washing up for the menu check, so a restaurant with a rack needs no sink.

### Queues and patience

Every group in the queue has its own patience bar. When it runs out, that group walks away and the restaurant loses
reputation. With Take-Out on, the patience left when a customer reaches the counter carries over to waiting for their
order and their food.

### Recipes, Recipe Mastery and the recipe shop (Smokey)

- Smokey sells recipes in three sections: main dishes, starters and desserts, and add-ons for a dish.
- Every dish you serve builds that recipe's **Recipe Mastery**. Each level unlocks the recipe's next add-on as a card
  in the Card Manager, so add-ons are earned by cooking, not bought.
- Harder dishes, add-ons you have switched on, a busy kitchen and better ingredients all speed Mastery up. Starters and
  desserts count towards your mains, and recipes you rarely serve catch up a little each night.

### Kitchen load: bigger menus, fewer but richer customers

Every recipe on the menu adds **kitchen load**. A bigger, harder menu means fewer customers, but each one pays more.
Every main dish beyond your first brings noticeably fewer customers who each pay a lot more. The tag on each switched-on
recipe card in the Card Manager shows how many fewer customers it brings. Automation appliances and extra players
lighten the load.


### Ingredient quality (2 stars)

| Tier | Restocking | Daily fee |
|---|---|---|
| Budget | lowest | none |
| Standard | a little more | none |
| Premium | more | 15 coins |
| Gourmet | most | 30 coins |

Better ingredients earn more reputation, tips and Recipe Mastery; cheaper ones save money. From 3 stars customers
expect better ingredients with every star (Standard at 3, Premium at 4, Gourmet at 5), and serving below that costs
reputation and tips. The Management Desk shows roughly what each tier would cost a day at yesterday's takings. With
the card switched off, the restaurant runs on Budget ingredients.

### Ambience and decor

Furniture, wallpaper and flooring that customers can see (dining rooms, entrances and any room with a table or the
Ordering Counter) give the restaurant **looks**. Each star level expects a nicer place. Below expectations, the day's
serving reputation shrinks; above them it grows. The Reputation Report shows the result as **Ambience**.

### The nightly bill

At closing you pay utilities and upkeep for your appliances, restocking, licences, cleaning for any mess or full bins
left, staff wages when friends work with you, any loan payment, and any health inspection fine. The ledger shows every
row.

### Health inspector

On rare days a health inspector tours the restaurant, silently. A mess or full bin they spot is forgiven if it's cleaned
within a few seconds (6 playing alone, 3 with friends). Anything still there after that costs
reputation, more the higher your reputation, plus a fine. A spotless visit where they were also served earns a bonus.

### Multiplayer

Every extra player is staff: they draw a paycheck every night that grows with stars and takings. More players also
share the kitchen load and bring more customers, as in the regular game.

### Reporting a bug

Tycoon Mode keeps its own short log, `TycoonLog.txt`, in the same folder as the game's Player.log
(`%USERPROFILE%\AppData\LocalLow\It's Happening\PlateUp`). Send it with your bug report, and `TycoonLog-previous.txt`
too if you restarted the game since the problem. In multiplayer, the host's file matters most; send both if you can.

---

## Formulas and balancing

All numbers below are from the current build. "Stars" means the restaurant's star level (0 to 5); lists such as
`{a, b, c, d, e, f}` are indexed by stars 0 to 5. Rounding is done the way the game shows it (usually up for costs).

### Design intent in brief

- Each star should take longer than the one before. The reputation totals were tuned in
  simulation so the gaps between stars grow roughly evenly (about 3, 5, 7, 8 and 10 days), with 3 to 4 stars taking
  around 7 to 10 days. A solo player reaches 5 stars in roughly 31 to 34 days, whichever strategy they follow.
- A huge menu of mains, full multi-course meals, a crowd-heavy
  restaurant built on customer cards, and a mix of everything are all balanced to be equally viable with one another. The card
  values are tuned so that no single card type is the fast lane. A skilled player who fills a big menu *and* a big crowd
  at once will be able to reach 5-stars the fastest.
- Serving alone can't beat the nightly decay at high stars; you need cards switched on. About
  12 to 16 cards reach 5 stars, or about 7 if they're mostly mains.
-  Loans, nightly bills, appliance upkeep, ingredient quality, marketing, contracts and new
  restaurants all compete for the same wallet, so mid- and late-game income always has somewhere to go.
- Risk is rewarded. Harder cards, heat cards, contracts and bigger groups pay more reputation because they make the
  restaurant harder to run.
- Wages and per-player scaling keep 2 to 4 players roughly level with solo players.

### Customer traffic

- **Star crowd:** at exactly a star's reputation total, a Tycoon day is as busy as regular PlateUp! day `1 + 3 × stars`.
- **Reputation moves traffic in a straight line between star crowds:**
  `customers = lerp(crowd(stars), crowd(stars + 1), progress)`, where
  `progress = (reputation - total(stars)) / (total(stars + 1) - total(stars))`, clamped to 0..1.
  Progress stops at 1: past the next star's total the crowd stays at the next star's until the critic awards the star.
  At 5 stars there is no next star, so no extra crowd.
- **Below your star's total:** the same line runs down to the star below's crowd at that star's total, so a demotion
  doesn't change the day's crowd. At 0 stars and negative reputation, traffic falls towards `crowd(0)² / crowd(1)` at
  -100.
- **Coffee Shop card** (the coffee stand starts with it) and **Take-Out:** `floor(5 × 1.15^stars)` extra guests a day
  (×1.15 in multiplayer), half for coffee with the Coffee Shop card, the rest with Take-Out.
- **Kitchen load cut:** `customers × 1 / (1 + 0.5 × addedMains) / (1 + 0.036 × otherLoad)` (see Kitchen load).
- **Crowd size:** the day's total is then ×0.9 solo, ×0.85 in multiplayer.
- **Marketing and crowd cards** add their percentage on top.

*Intent:* the crowd grows steadily as reputation climbs, so there's no sudden jump on the day a star is earned. Traffic
can't run ahead of the stars: failing a critic on purpose to stay on a cheaper star level never brings more customers
than the next star would.

### Kitchen load

- **Load of one recipe:** `difficulty (1 to 5) × weight`, with weight 1 for a main, 0.6 for a starter, side or dessert,
  and 0.3 for an extra.
- **Automation:** every automation appliance takes 0.5 off, up to half of the load.
- **Players:** `load / sqrt(players)` and `addedMains / sqrt(players)`.
- **Added mains:** main recipes (Base dishes such as Burgers, Coffee, Sundaes) on the menu beyond the first.
- **Customers:** `× 1 / (1 + 0.5 × addedMains)`, so the first added main brings 33% fewer customers, then 50%, 60%,
  67% fewer… Then `× 1 / (1 + 0.036 × otherLoad)` for everything that isn't a main; a side counts weight 0.9 here
  (0.6 + 0.3) instead of 0.6.
- **Prices:** `+ load × (0.04 × mainShare + 0.031 × (1 − mainShare)) + 0.57 × addedMains`, where `mainShare` is the
  share of the load that comes from mains.

*Intent:* a big menu shouldn't just multiply income and reputation. Each added main costs about 30% of the crowd but
raises prices by half, so a Variety restaurant serves a smaller, richer clientele. A strict ×0.7 per main made a
mains-only menu unplayable in simulation, so the cut is hyperbolic instead.

### What a dish pays

For each item served, in order:

1. The game's price × **1.5** (× 0.7 at an Ordering Counter with Take-Out).
2. **+ (1 + difficulty)** coins for a dish that isn't a main (starters, sides, desserts, extras).
3. **+ 1 coin per star.**
4. **+ price bonuses** as a percentage of the item: Marketing, the kitchen-load bonus above, +25% with Five-Star Menu
   Prices, +40% on a luxury contract day.
5. **+ 15% per course** after the customer's first (the Full Meal bonus).
6. **+ 20% of the item's price for every extra** in the order (Extra Milk, Extra Sugar…), paid separately.
7. **Tips**
   `chance = lerp(5%, 60%, level / 5)` (+ Marketing, at most 95%),
   `size = lerp(5%, 25%, level / 5)` of the payment, where `level = stars + progress towards the next star`.
   Then × the ingredient quality's tip multiplier.

*Intent:* early coffee days roughly break even; income grows with stars, menu complexity and service quality rather
than with raw volume.

### Reputation per serve

```
gain = 1                                    (base)
     + patienceModifier                     (-3 .. +3)
     - messLoss                             (below)
     + cardBonus                            (sum of every switched-on card's value)
     + Marketing reputation per serve
     + 1.5 on a luxury contract day
if gain > 0:  gain × volumeFactor
served on a Disposable Plate:  gain × 0.5
```

**Mess loss** (each floor mess at the moment of the serve, at most 6; a wet floor from the mop counts as a quarter of a mess):

```
before   = 1 + patienceModifier + cardBonus
messLoss = min(messes, 6) × max(0.5, (0.03 + 0.01 × stars) × before)
```

*Intent:* a mess has to hurt as much at 5 stars as at 1. As a share of the serve, it grows with the cards that earn the
reputation and with the stars (3% a mess at 0 stars, 8% at 5), so six messes at 5 stars cost nearly half of every
serve. Cleaning during the day always pays off, however rich the restaurant is.

- **Patience modifier:** 0 at 50% patience left, rising linearly to +3 at full patience and falling to −3 at none.
  A serve with little patience left loses reputation overall.
- **Volume factor:** `clamp(24 / todaysServes, 0.25, 3 × (1 + 0.5 × addedMains))`, where
  `todaysServes = today's customers × serves per customer` (measured each night at this restaurant, starting at 1.25).
  A fully served day earns about the same whatever the crowd, so each customer of a small crowd is worth more. The
  ceiling grows with added mains so a big menu's small crowd still earns full reputation.
- **Walkouts:** a group that runs out of queue patience costs **15**. A Fast Casual customer who found no table costs **6**.

*Intent:* reputation rewards quality (speed, cleanliness, cards), not throughput. Measuring against serves rather than
customers stops add-ons from double-dipping.

### Card values (reputation per serve while switched on)

| Card | Value |
|---|---|
| Main recipe | `5.2 + 3.9 × difficulty` |
| Other recipe (starter, side, dessert, extra) | `2.16 + 2.16 × difficulty` |
| Game customer card | `{1.1, 2.2, 4.5, 6.7}` by the game's XP reward (none / small / medium / large), +1.1 or +2.2 if it brings more customers |
| Heat card | 16 |
| Chubs's own cards | set in `CustomerCards.txt` (2.6 to 12.5, about 0.25 per 1% more customers) |
| Group-size cards | value × **seated share** (below) |
| Take-Out, Fast Casual, star reward cards | 0 |

**Seated share** for group-size cards:

```
added    = sum of the switched-on cards' increases to the largest group
overflow = max(0, largest possible group − largest table)
share    = clamp((added − overflow) / added, 0, 1)
```

*Intent:* the game never sends a group bigger than your largest table, so without this a group card would be free
reputation. Players only earn a group card's value once they have built for it.

*Intent of the values:* each strategy leans on one card type (Variety on mains, Full Meal on starters and desserts, Crowd
on Chubs's own cards), so each type's value sets that strategy's pace. They were tuned together in the simulation until
all four reached 5 stars between day 31 and 34. The game's own customer cards are worth less because they come cheap and
every strategy can top up with them. Heat cards keep their high value as the reward for a much harder restaurant.

### Nightly reputation steps (at closing, in order)

1. **Ambience** (see below): `servingReputationToday × ambience%`.
2. **Ingredient quality** (see below): `servingReputationToday × quality%`.
3. **Full House bonus:** if at least 95% of customers were served, the crowd was bigger than normal, and the kitchen
   load is at least 15:
   `bonus% = min(60%, 0.3 × (customers / normalCrowd − 1) × load / 15)` of the day's serving reputation.
   `normalCrowd = {14, 15, 17, 20, 25, 35} × (1 + 0.5 × (players − 1))`.
4. **Decay:** `{5, 15, 60, 190, 380, 600} + {2, 3, 5, 6, 8, 15}% × (reputation above your star's total)`,
   × 1.15 from 3 stars, minus any Marketing decay cut.
5. **Health inspection** result (if one came): per mess or full bin found and not cleaned within the grace period,
   −(10 + 0.005 × reputation total) reputation (about 15 at 1 star, 90 at 5) and a 5-coin fine × the cost multiplier;
   +20 for a spotless visit where the inspector was served. Grace period: 6 seconds solo, 3 with friends.
6. **Critic verdict** (see Stars).

*Intent:* the decay grows faster than serving alone can keep up with, so higher stars require cards, decor and good
ingredients. The Full House bonus rewards the player who handles both a big crowd and a big menu.

### Stars

- **Reputation total for each star:** `{0, 100, 1000, 2900, 6650, 15850}`. Reputation is one running total for the
  restaurant and carries on through star changes.
- **New star:** reaching the next total books a critic for the next day. Serve them without a net loss and the star is
  earned at closing.
- **Star at risk:** a critic comes once the total falls 25% of the way back into the band below
  (`total(stars) − 0.25 × (total(stars) − total(stars − 1))`). An unhappy critic, or one never served, takes the star.
- **Floor:** reputation never drops below −100.

*Intent:* gaps between stars grow evenly so early stars come quickly and later ones are a real goal.

### Ambience

```
looks    = 2.5 per 100 coins of visible furniture + the tier value of each visible room's wallpaper and flooring
expected = {0, 2, 5, 9, 14, 20}
ratio    = looks / expected
below expectations:  ambience = −{0, 10, 20, 25, 30, 35}% × (1 − ratio)
above expectations:  ambience = min(+30%, 30% × (ratio − 1))      (full bonus at twice the expectation)
```

Wallpaper and flooring tiers cost `{50, 90, 140, 220}` coins and are worth `{1, 2, 3, 5}` looks.

*Intent:* decor used to be ignorable; now it scales the whole day's reputation. The penalty ceiling (35% at 5 stars) is
deliberately mild: a restaurant with no decor progresses slowly but never gets stuck.

### Ingredient quality

| | Budget | Standard | Premium | Gourmet |
|---|---|---|---|---|
| Restocking share of takings | 15% | 22% | 30% | 40% |
| Daily fee | 0 | 0 | 15 | 30 |

- **Expected tier by stars:** `{Budget, Budget, Budget, Standard, Premium, Gourmet}`.
- **Per tier above expectations:** +30% of the day's serving reputation, tips × 1.2, Recipe Mastery × 1.15. Steps stack.
- **Per tier below expectations:** −20% of the day's serving reputation, tips × 0.75. Steps stack.
- **Card off or below 2 stars:** Budget.

*Intent:* a real choice between profit and progress. In simulation, Budget makes the most money; one tier above
expectations is slightly faster to 5 stars at a fair cost; two tiers above loses money for a young first restaurant
and is meant for later restaurants that the company can bankroll. Gourmet from day one goes bankrupt. Standard has no
fee because its higher restocking share is already its cost; a fee on top made it strictly worse than Budget.

### The nightly bill

`costMultiplier = 1 + 0.25 × stars`

| Row | Formula |
|---|---|
| Utilities | `0.5 × appliances owned × costMultiplier` |
| Upkeep | `1% × total price of appliances owned` |
| Restocking | `takings × the ingredient tier's share + 1 per ingredient crate or dispenser + the tier's daily fee + 1 × costMultiplier per Disposable Plate used` |
| Licences | `3 × costMultiplier` |
| Dirty floors | `(1 + 0.002 × reputation) × messes left at closing × costMultiplier` (about 2.5 a mess at 1 star, 12 at 3, 75 at 5; the mop's wet floor is not a mess) |
| Trash left | `(0.5 + 0.0003 × reputation) × items left in bins × costMultiplier` (about 1 an item at 1 star, 3 at 3, 12 at 5) |
| Staff wages | `(players − 1) × (1 × (1 + stars) + (0.35 + 0.03 × stars) × takings / players)` |
| Loan payment | the plan's daily payment (none for the first 3 closings after a loan or lease) |
| Inspection fine | 5 per problem found |

There is no rent: sites are bought or leased, and the first restaurant is free.

*Intent:* the coffee stand roughly breaks even at first; buying equipment without growing sales slowly bleeds cash.
Wages bring 2 to 4 player groups level with a solo player.

### Shop prices

- **Purchasing Desk:** prices come from `AppliancePrices.txt`. An appliance not listed there costs the game's price ×
  its category (ingredients 0.9, kitchen 1.5, cleaning and dining room 2, cooking and decorations 2.5, automation and
  research 4), × 3 for a first upgrade and × 5 for an upgrade of an upgrade, rounded to 5 coins.
- **Earnings rate:** `rate = 0.35 + 0.65 × averageProfit / 150`, clamped to 0.35 to 1.3, where `averageProfit` is the
  company's average profit over the last 5 closings.
  - **Chubs:** card price × rate.
  - **Purchasing Desk:** price × `0.75 + 0.25 × earnings`, clamped to 0.75 to 1.3.
  - **Smokey:** price × `min(rate, 0.1 + 0.9 × totalReputation / 3000)`, rounded to 10 coins.
- **Smokey's full prices:** mains `150 + 50 × difficulty`; others `280 + 140 × difficulty`.
- **Chubs's full prices:** `100 + 70 × card value`, rounded to 10 (his own cards use the price in `CustomerCards.txt`).
- **Whiskers:** 25% to 60% off, in steps of 5%. Two tries, each with a 50% chance, to stock an appliance your recipes
  still need.
- **Selling:** an appliance sold at the curb returns 50%; bankruptcy sells appliances and sites back at 50%.

*Intent:* a struggling company can still afford to grow, while a rich one pays full price, so money stays meaningful.

### Bank

- **Credit:** for each restaurant, `300 × (1 + stars) + 0.14 × its positive reputation`, added together. A company
  with no restaurant yet has 300.
- **Loan offer:** the whole credit + 200, rounded down to 10.
- **Bank tier:** `clamp((credit − 300) / (3000 − 300), 0, 1)`: gentle terms for a small company, tough ones for a big
  company.
- **Plans:** from `{10, 15, 20, 25, 30}` days (gentle) to `{5, 10, 15, 20, 30}` (tough).
- **Interest:** `lerp(3%, 6%, tier) + lerp(0.15%, 0.4%, tier) × (days + 3)`; half for the company's first personal loan.
  The `+ 3` pays for the 3 payment-free nights every new loan and lease starts with. For example, gentle 10 days 4.95%,
  30 days 7.95%; tough 10 days 11.2%, 30 days 19.2%. A plan change uses the days alone.
- **Daily payment:** `ceil(owed / days)`.
- **Sites:** `(400 + 25 × sqm) × location factor`, rounded to 50. Factors: City 1.25, Alpine and Japan 1.15,
  Autumn 0.9, Country and Farm 0.85.
- **Starting money:** 150 coins.

### Recipe Mastery

- **Points per item served:** `(1 + 0.25 × (dish tier − 1) + 0.2 × add-ons switched on) / customerMultiplier ×
  ingredientMasteryMultiplier`. Dividing by the kitchen load's customer multiplier cancels the crowd a big menu
  loses; the ingredient multiplier is × 1.15 per tier above expectations.
- A starter, side or dessert without its own track splits its points across the mains on the menu, and every main
  also gets 10% of the others' points.
- **Nightly catch-up:** a recipe served fewer than 8 times today gets `sqrt(8 / serves) − 1` × its day's points more
  (at most ×3 in all).
- **Serves needed for the next level:** `40 × 1.75^level × (1 + 0.25 × (next add-on's tier − 1))`.

*Intent:* big Variety and Full Course menus split their serves across many recipes. Without these corrections they
mastered recipes several times slower than a one-main menu; with them, every strategy unlocks add-ons at a similar pace.

### Take-Out, Fast Casual and queues

- **Take-Out:** an Ordering Counter order pays × 0.7; a Disposable Plate serve earns × 0.5 reputation.
- **Fast Casual:** half of the arriving parties eat at a table. A customer who finds no table waits to the side on
  their remaining patience. Leftover dirty dishes go on the table, with a 25% chance of a mess (× the customer's own
  mess factor).
- **Queue patience:** each group has the game's queue patience time, + Marketing seconds, × 0.85 with Five-Star Menu
  Prices, × the contract's patience % for contract guests. It doesn't drain while the friend they arrived with is just
  ahead of them or at the counter.

### Marketing and contracts

- **Marketing** (3 stars): 3 offers a day, up to 3 campaigns at once. Each campaign's cost, length and effects
  (customers, reputation per serve or per day, tip chance, prices, queue patience, decay cut) are listed in
  `MarketingCards.txt`.
- **Contracts** (4 stars): 4 offers a day, one booked at a time. Booking takes a deposit of **25%** of the reward coins
  (**40%** for a luxury contract), returned on success or cancellation.
  - **Failure:** you lose the reputation and coins the contract lists, the deposit, and everything the contract guests
    paid and earned that day.
  - **Luxury contract** (5 stars): a whole day of VIPs paying +40% and earning +1.5 reputation a serve. If it fails,
    the VIPs' extra pay and reputation are taken back as well.

*Intent:* contracts are a real gamble rather than free money, and the luxury deposit and penalty match the bigger
reward.

---

## Editing the numbers

Most of the content can be changed without touching code. Edit the files in `Resources/`, then rebuild the mod:

| File | What it holds |
|---|---|
| `AppliancePrices.txt` | Purchasing Desk prices by appliance name. |
| `CustomerCards.txt` | Chubs's own cards (customers %, group size, reputation, price, flavour) and the game cards he never sells. |
| `MarketingCards.txt` | Marketing campaigns. |
| `Contracts.txt` | Contracts and luxury contracts. |
| `TycoonGuide.txt` | The guide pages shown at HQ and at each star. |
| `*Dialogue.txt` | What the HQ cats say. |

The formulas above are constants in the source, each documented next to its value (`TycoonEconomy.cs`,
`TycoonReputation.cs`, `TycoonIngredientQuality.cs`, `TycoonBank.cs`, `TycoonShopPrices.cs`,
`Systems/TycoonKitchenLoadSystem.cs`, `Systems/TycoonRecipeMasterySystem.cs`).

The timings quoted under *Intent* come from offline simulations of the economy, not from measured play, and will
shift as the game is tested.

---

## Building from source

You need the [.NET SDK](https://dotnet.microsoft.com/download) and PlateUp! with KitchenLib and Harmony subscribed on
the Steam Workshop.

1. The game's own DLLs are not part of this repository. Copy the files listed in [lib/README.md](lib/README.md) from
   your PlateUp! install and the KitchenLib Workshop folder into `lib/`.
2. If PlateUp! is not in the default Steam folder, change `PlateUpGameFolder` in `PlateUpTycoon.csproj`.
3. Close PlateUp!, then run `dotnet build PlateUpTycoon.csproj -c Release`. The build copies `PlateUpTycoon.dll` into
   the game's `Mods\PlateUpTycoon\content` folder.

---

## Licence

Tycoon Mode is free software under the [GNU General Public License v3.0](LICENSE). PlateUp! and its assets belong to
their owners and are not covered by this licence.
