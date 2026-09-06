/**
 * Atlas Board chat profanity filter.
 *
 * This list is deliberately maintained in one file so moderation vocabulary
 * can be reviewed without touching chat transport logic. Terms are normalized
 * before comparison (case, common diacritics, and basic leetspeak).
 *
 * Languages currently covered by the product:
 * English, Turkish, Spanish, French, German, Korean, Russian.
 */

const blockedTermsByLanguage: Record<string, string[]> = {
  en: [
    "fuck", "fucker", "fucking", "motherfucker", "shit", "bullshit",
    "bitch", "asshole", "bastard", "dickhead", "cunt", "prick",
  ],
  tr: [
    "sik", "siktir", "siktirgit", "orospu", "orospucocugu", "amcik",
    "amina", "aminakoyim", "amk", "aq", "pic", "yarrak", "got",
    "pezevenk", "ibne",
  ],
  es: [
    "puta", "puto", "mierda", "joder", "cabron", "cabrona", "cojones",
    "pendejo", "pendeja", "gilipollas", "maricon",
  ],
  fr: [
    "merde", "putain", "connard", "connasse", "salope", "encule",
    "enculee", "batard", "bite",
  ],
  de: [
    "scheisse", "arschloch", "fotze", "wichser", "hurensohn", "bastard",
    "fick", "ficken", "schlampe",
  ],
  ko: [
    "씨발", "시발", "개새끼", "병신", "좆", "존나", "지랄", "썅",
  ],
  ru: [
    "блядь", "блять", "сука", "хуй", "пизда", "ебать", "ёбать",
    "мудак", "долбоеб", "долбоёб", "шлюха",
  ],
};

const leetMap: Record<string, string> = {
  "0": "o",
  "1": "i",
  "3": "e",
  "4": "a",
  "5": "s",
  "7": "t",
  "@": "a",
  "$": "s",
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

const normalizedBlockedTerms = Array.from(
  new Set(
    Object.values(blockedTermsByLanguage)
      .flat()
      .map(normalize)
      .filter((term) => term.length > 0),
  ),
);

/**
 * Escapes a literal for use inside a RegExp.
 * @param {string} value Literal text to escape.
 * @return {string} RegExp-safe literal text.
 */
function escapeRegex(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/**
 * Builds a punctuation-tolerant word pattern, so simple disguises such as
 * f.u.c.k do not bypass the list while ordinary longer words remain safe.
 * @param {string} term Normalized blocked term.
 * @return {RegExp|null} Obfuscation pattern or null for unsupported terms.
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

const obfuscatedPatterns = normalizedBlockedTerms
  .map((term) => ({term, regex: obfuscatedWordRegex(term)}))
  .filter((item) => item.regex !== null) as Array<{
    term: string;
    regex: RegExp;
  }>;

/**
 * Returns true when the supplied message contains a blocked term.
 * @param {string} message Chat message to inspect.
 * @return {boolean} True when blocked profanity is detected.
 */
export function containsBlockedProfanity(message: string): boolean {
  const normalized = normalize(message);
  if (!normalized) return false;

  const tokens = normalized
    .split(/[^\p{L}\p{N}]+/u)
    .filter((token) => token.length > 0);

  const tokenSet = new Set(tokens);

  for (const term of normalizedBlockedTerms) {
    if (!term.includes(" ") && tokenSet.has(term)) {
      return true;
    }

    if (term.includes(" ")) {
      const phrase = term.replace(/\s+/g, " ");
      const messageWithSpaces = normalized
        .replace(/[^\p{L}\p{N}]+/gu, " ")
        .replace(/\s+/g, " ")
        .trim();

      if (` ${messageWithSpaces} `.includes(` ${phrase} `)) {
        return true;
      }
    }
  }

  for (const item of obfuscatedPatterns) {
    if (item.regex.test(normalized)) {
      return true;
    }
  }

  return false;
}

/**
 * Returns language keys exposed for tests and moderation tooling.
 * @return {string[]} Configured profanity language keys.
 */
export function profanityLanguageKeys(): string[] {
  return Object.keys(blockedTermsByLanguage);
}
