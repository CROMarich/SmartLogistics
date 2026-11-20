# Tehnički zadatak – Smart Logistics Planner

## Cilj

Web-aplikacija koja optimizira prijevoz radnih strojeva između lokacija:

- dodjela naloga kamionima,
- redoslijed preuzimanja i isporuka,
- minimalna ukupna kilometraža.

Obavezan je povrat u bazu.

## Operativna ograničenja

- Vozač smije voziti **9 h dnevno**.
- Utovar/istovar traje **20 min**.
- Brzina je **60 km/h**.

## Kamioni

Oba kamiona kreću iz baze **L0**.

| Kamion | Nosivost (kg) | Max. broj strojeva |
|--------|--------------:|-------------------:|
| A      | 3500          | 2                  |
| B      | 10000         | 3                  |

## Strojevi

| Stroj | Masa (kg) |
|-------|----------:|
| M01   | 800       |
| M02   | 1300      |
| M03   | 1800      |
| M04   | 2400      |
| M05   | 3100      |
| M06   | 3900      |
| M07   | 4800      |
| M08   | 6100      |
| M09   | 7500      |
| M10   | 9400      |

## Lokacije

Koordinate X, Y u km.

| Lokacija | X  | Y  | Napomena |
|----------|---:|---:|----------|
| L0       | 0  | 0  | baza     |
| L1       | 3  | 8  |          |
| L2       | 10 | 11 |          |
| L3       | 17 | 9  |          |
| L4       | 9  | -6 |          |
| L5       | 26 | 3  |          |
| L6       | -7 | 13 |          |
| L7       | 20 | -8 |          |
| L8       | 23 | 15 |          |
| L9       | -8 | -5 |          |

Udaljenost je **euklidska**, a svaka dionica se zaokružuje na **1 decimalu**.

## Nalozi

| Nalog | Stroj | Preuzimanje | Isporuka |
|-------|-------|-------------|----------|
| N01   | M01   | L1          | L3       |
| N02   | M02   | L2          | L5       |
| N03   | M03   | L9          | L4       |
| N04   | M04   | L3          | L0       |
| N05   | M05   | L0          | L8       |
| N06   | M06   | L5          | L7       |
| N07   | M07   | L8          | L2       |
| N08   | M08   | L4          | L9       |
| N09   | M09   | L6          | L1       |
| N10   | M10   | L7          | L3       |

## Funkcionalni zahtjevi

Aplikacija mora:

1. prikazati ulazne podatke;
2. rasporediti naloge na kamione;
3. odrediti redoslijed;
4. nikad ne prekoračiti nosivost ni broj strojeva;
5. osigurati da isti kamion preuzima i isporučuje, uz preuzimanje prije isporuke;
6. minimizirati kilometražu;
7. prikazati km po kamionu i ukupno;
8. jasno upozoriti na neizvedive naloge.

Po kamionu prikazati:

- redoslijed lokacija,
- radnje,
- teret nakon svake radnje,
- km,
- broj naloga.

Karta je opcionalna.

## Predaja

- kod,
- upute,
- demo,
- opis logike,
- minimalno 3 testa poslovnih ograničenja,
- osvrt na AI alate,
- utrošeno vrijeme.
