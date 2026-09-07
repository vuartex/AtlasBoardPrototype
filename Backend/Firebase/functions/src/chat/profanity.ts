/**
 * Atlas Board chat profanity filter.
 *
 * The static vocabulary lives in this file so the moderation baseline can be
 * reviewed independently from chat transport. The server can also provide
 * extra blocked terms through Firestore without requiring another deploy.
 *
 * Product languages:
 * English, Turkish, Spanish, French, German, Korean, Russian.
 */

const blockedTermsByLanguage: Record<string, string[]> = {
  en: [
    "fuck",
    "fucker",
    "fuckers",
    "fucking",
    "motherfucker",
    "motherfuckers",
    "shit",
    "shitty",
    "bullshit",
    "bitch",
    "bitches",
    "asshole",
    "assholes",
    "bastard",
    "bastards",
    "dickhead",
    "dickheads",
    "dumbass",
    "jackass",
    "cunt",
    "cunts",
    "prick",
    "pricks",
    "wanker",
    "twat",
    "slut",
    "whore",
    "cockhead",
    "dipshit",
    "douchebag",
    "sonofabitch",
  ],
  tr: [
    "sik",
    "sikerim",
    "sikeyim",
    "siktir",
    "siktirgit",
    "siktirlan",
    "siktirsin",
    "orospu",
    "orospular",
    "orospucocugu",
    "amcik",
    "amcigin",
    "amina",
    "aminakoyim",
    "aminakoyayim",
    "aminakoydugum",
    "amk",
    "aq",
    "pic",
    "picler",
    "pich",
    "yarrak",
    "yarak",
    "gotveren",
    "gotlek",
    "pezevenk",
    "ibne",
    "kahpe",
    "kaltak",
    "serefsiz",
    "haysiyetsiz",
    "dallama",
    "dingil",
    "gerizekali",
  ],
  es: [
    "puta",
    "putas",
    "puto",
    "putos",
    "mierda",
    "joder",
    "jodete",
    "cabron",
    "cabrones",
    "cabrona",
    "cojones",
    "pendejo",
    "pendejos",
    "pendeja",
    "gilipollas",
    "maricon",
    "maricona",
    "culero",
    "culera",
    "chingar",
    "chingada",
    "chingado",
    "chingatumadre",
    "verga",
    "pinche",
    "mamaguevo",
    "hijodeputa",
  ],
  fr: [
    "merde",
    "putain",
    "connard",
    "connards",
    "connasse",
    "salope",
    "salopes",
    "encule",
    "encules",
    "enculee",
    "batard",
    "batards",
    "bite",
    "couille",
    "couilles",
    "branleur",
    "branleuse",
    "trouduc",
    "filsdepute",
    "nique",
    "niquetamere",
  ],
  de: [
    "scheisse",
    "scheiß",
    "scheiße",
    "arschloch",
    "arschloecher",
    "fotze",
    "fotzen",
    "wichser",
    "hurensohn",
    "hurensoehne",
    "bastard",
    "fick",
    "ficken",
    "fickdich",
    "schlampe",
    "schlampen",
    "spast",
    "vollidiot",
    "drecksack",
    "miststueck",
    "verpissdich",
  ],
  ko: [
    "씨발",
    "시발",
    "씨발놈",
    "시발놈",
    "개새끼",
    "개새",
    "병신",
    "븅신",
    "좆",
    "좆같",
    "존나",
    "지랄",
    "썅",
    "미친놈",
    "미친년",
    "꺼져",
    "닥쳐",
    "개년",
    "개놈",
  ],
  ru: [
    "блядь",
    "блять",
    "блядина",
    "сука",
    "сучка",
    "хуй",
    "хуи",
    "хуесос",
    "пизда",
    "пиздец",
    "ебать",
    "ёбать",
    "ебаный",
    "ёбаный",
    "еблан",
    "мудак",
    "мудаки",
    "долбоеб",
    "долбоёб",
    "шлюха",
    "гандон",
    "говно",
    "дерьмо",
    "нахуй",
    "пошелнахуй",
    "пошёлнахуй",
  ],
};

const leetMap: Record<string, string> = {
  "0": "o",
  "1": "i",
  "2": "z",
  "3": "e",
  "4": "a",
  "5": "s",
  "6": "g",
  "7": "t",
  "8": "b",
  "9": "g",
  "@": "a",
  "$": "s",
  "!": "i",
};

/**
 * Normalizes a term for moderation comparison.
 * @param {string} value Term or message fragment to normalize.
 * @return {string} Normalized moderation text.
 */
function normalize(value: string): string {
  const mapped = Array.from(value.toLowerCase())
    .map((character) => leetMap[character] ?? character)
    .join("");

  return mapped
    .normalize("NFKD")
    .replace(/\p{M}+/gu, "")
    .replace(/ı/g, "i")
    .replace(/ё/g, "е")
    .trim();
}

/**
 * Escapes a literal for use inside a regular expression.
 * @param {string} value Literal text to escape.
 * @return {string} Regular-expression-safe literal.
 */
function escapeRegex(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/**
 * Builds a punctuation-tolerant word pattern for one blocked term.
 * @param {string} term Normalized blocked term.
 * @return {RegExp|null} Pattern, or null when the term is too short.
 */
function obfuscatedWordRegex(term: string): RegExp | null {
  const letters = Array.from(term);
  if (letters.length < 4 || letters.some((item) => item === " ")) {
    return null;
  }

  const body = letters
    .map((letter) => escapeRegex(letter))
    .join("[^\\p{L}\\p{N}]*");

  return new RegExp(
    `(^|[^\\p{L}\\p{N}])${body}($|[^\\p{L}\\p{N}])`,
    "u",
  );
}

/**
 * Creates the normalized unique term set used by the detector.
 * @param {string[]} extraTerms Optional server-configured blocked terms.
 * @return {string[]} Unique normalized terms.
 */
function buildTerms(extraTerms: string[]): string[] {
  const staticTerms: string[] = [];
  for (const terms of Object.values(blockedTermsByLanguage)) {
    staticTerms.push(...terms);
  }

  return Array.from(
    new Set(
      staticTerms
        .concat(extraTerms)
        .map(normalize)
        .filter((term: string) => term.length > 0),
    ),
  );
}

/**
 * Tests one normalized message against a normalized blocked-term list.
 * @param {string} normalized Normalized message.
 * @param {string[]} terms Normalized blocked terms.
 * @return {boolean} True when a blocked term is found.
 */
function testTerms(normalized: string, terms: string[]): boolean {
  const tokens = normalized
    .split(/[^\p{L}\p{N}]+/u)
    .filter((token) => token.length > 0);
  const tokenSet = new Set(tokens);
  const messageWithSpaces = normalized
    .replace(/[^\p{L}\p{N}]+/gu, " ")
    .replace(/\s+/g, " ")
    .trim();

  for (const term of terms) {
    if (!term.includes(" ") && tokenSet.has(term)) {
      return true;
    }

    if (term.includes(" ")) {
      const phrase = term.replace(/\s+/g, " ");
      if (` ${messageWithSpaces} `.includes(` ${phrase} `)) {
        return true;
      }
    }

    const regex = obfuscatedWordRegex(term);
    if (regex != null && regex.test(normalized)) {
      return true;
    }
  }

  return false;
}

/**
 * Returns true when the supplied message contains a blocked term.
 * @param {string} message Chat message to inspect.
 * @param {string[]} extraTerms Optional backend-configured blocked terms.
 * @return {boolean} True when blocked profanity is detected.
 */
export function containsBlockedProfanity(
  message: string,
  extraTerms: string[] = [],
): boolean {
  const normalized = normalize(message);
  if (!normalized) return false;
  return testTerms(normalized, buildTerms(extraTerms));
}

/**
 * Returns language keys exposed for tests and moderation tooling.
 * @return {string[]} Configured profanity language keys.
 */
export function profanityLanguageKeys(): string[] {
  return Object.keys(blockedTermsByLanguage);
}

/**
 * Returns the number of static terms for diagnostics and QA.
 * @return {number} Number of static configured terms.
 */
export function profanityStaticTermCount(): number {
  return buildTerms([]).length;
}
