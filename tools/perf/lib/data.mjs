import { randomUUID } from 'node:crypto';

const knownArtists = [
  'Die Ärzte', 'Böhse Onkelz', 'Mötley Crüe', 'Motörhead', 'Beyoncé',
  'Sigur Rós', 'Björk', 'Blümchen', 'Künstler Müller', 'Queen', 'ABBA',
  'Rammstein', 'Helene Fischer', 'Falco', 'Nena', 'Ärzte Cover Band',
  'Édith Piaf', "Sinéad O'Connor", 'André Rieu', 'Céline Dion',
  'Zoë Wees', 'Lena Meyer-Landrut', 'Münchener Freiheit', 'Jürgen Drews',
  'Andreas Gabalier', 'Die Toten Hosen', 'Kraftklub', 'Sportfreunde Stiller',
  'Wir sind Helden', 'Herbert Grönemeyer', 'Peter Maffay', 'Udo Lindenberg',
  'Nürnberg Symphonic', 'Måneskin', 'BAP', 'Héroes del Silencio',
  'Banda Bassotti', 'The Beatles', 'The Rolling Stones', 'David Bowie',
  'Elton John', 'Stevie Wonder', 'Aretha Franklin', 'María José',
  'Françoise Hardy', 'Käärijä', 'Tiziano Ferro', 'Róisín Murphy',
  'Chvrches', 'The Cranberries', 'AC/DC', 'Eurythmics', 'Tiësto',
  'Håkan Hellström', 'Mylène Farmer', 'P!nk', 'Robbie Williams',
  'Söhne Mannheims', 'Die Fantastischen Vier', 'EAV', 'Wanda',
  'Bilderbuch', 'AnnenMayKantereit', 'Wir sind Kaiser', 'Udo Jürgens',
  'Wolfgang Ambros', 'Rainhard Fendrich', 'La Oreja de Van Gogh',
  'João Gilberto', 'Günther & The Sunshine Girls'
];

const artistPrefixes = [
  'Amber', 'Arctic', 'Autumn', 'Blue', 'Bright', 'Crimson', 'Distant', 'Electric',
  'Fading', 'Golden', 'Hidden', 'Ivory', 'Jade', 'Lazy', 'Lunar', 'Midnight',
  'Neon', 'Northern', 'Ocean', 'Paper', 'Quiet', 'Radiant', 'Silver', 'Velvet',
  'Wild', 'Winter', 'Young', 'Broken', 'Cosmic', 'Dreaming', 'Emerald', 'Fever',
  'Gentle', 'Highland', 'Indigo', 'Kindred', 'Lost', 'Modern', 'Open', 'Royal'
];

const artistSuffixes = [
  'Arcade', 'Birds', 'Comets', 'Echo', 'Empire', 'Fables', 'Gardens', 'Harbor',
  'Hearts', 'Horizon', 'Lanterns', 'Letters', 'Lights', 'Machine', 'Marble',
  'Meadow', 'Mirrors', 'Motion', 'Orchestra', 'Parade', 'Pilots', 'Radio',
  'Rivers', 'Satellite', 'Signals', 'Stories', 'Sundays', 'Thunder', 'Voyage',
  'Wolves', 'Wonder', 'Assembly', 'Collective', 'Company', 'Corners', 'Futures',
  'Kingdom', 'Society', 'Sound', 'Station'
];

const titleWords = [
  'Über den Wolken', 'Schöne Welt', 'Für Elise', 'Straße', 'Weiß', 'Sommer',
  'Nacht', 'Herz', 'Lichter', 'Regen', 'Träume', 'Freiheit', 'Sterne', 'Meer',
  'Zeit', 'Wunder', 'Reise', 'Leben', 'Liebe', 'Himmel', 'Tanz', 'Echo',
  'Morgen', 'Erinnerung', 'Stadt', 'Melodie', 'Sonne', 'Wolken', 'Fluss',
  'Geheimnis', 'Heimat', 'Augen', 'Stille', 'Feuer', 'Weg', 'Klang', 'Mond',
  'Someday', 'Forever', 'Running', 'Tonight', 'Shadows', 'Paradise', 'Blue',
  'Summer', 'Dreams', 'Believe', 'Goodbye', 'Tomorrow', 'Wonderland'
];

function mulberry32(seed) {
  let state = seed >>> 0;
  return () => {
    state += 0x6D2B79F5;
    let value = state;
    value = Math.imul(value ^ (value >>> 15), value | 1);
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61);
    return ((value ^ (value >>> 14)) >>> 0) / 4294967296;
  };
}

function createArtistList(count) {
  const artists = [...knownArtists];
  for (const prefix of artistPrefixes) {
    for (const suffix of artistSuffixes) {
      const artist = `${prefix} ${suffix}`;
      if (!artists.includes(artist)) {
        artists.push(artist);
        if (artists.length === count) {
          return artists;
        }
      }
    }
  }

  while (artists.length < count) {
    artists.push(`Independent Artist ${artists.length + 1}`);
  }
  return artists.slice(0, count);
}

export function createSyntheticLibrary(count = 3700, seed = 0x4B415241) {
  if (!Number.isInteger(count) || count < 0) {
    throw new RangeError('Song count must be a non-negative integer');
  }

  const artists = createArtistList(1400);
  const random = mulberry32(seed);

  return Array.from({ length: count }, (_, index) => {
    const durationSeconds = 120 + Math.floor(random() * 211);
    const isVideo = index % 6 === 0;
    const title = `${titleWords[index % titleWords.length]} ${titleWords[(index * 17 + 11) % titleWords.length]}`;
    const metadata = isVideo
      ? { mediaType: 'video', extension: '.mp4', durationSeconds, width: 1920, height: 1080 }
      : { durationSeconds };

    return {
      id: randomUUID(),
      artist: artists[index % artists.length],
      title,
      metadataJson: JSON.stringify(metadata)
    };
  });
}
