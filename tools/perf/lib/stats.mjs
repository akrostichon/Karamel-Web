function nearestRank(sortedNumbers, percentile) {
  return sortedNumbers[Math.ceil(percentile * sortedNumbers.length) - 1];
}

export function summarize(numbers) {
  if (!Array.isArray(numbers)) {
    throw new TypeError('summarize expects an array of numbers');
  }
  if (numbers.some(number => !Number.isFinite(number))) {
    throw new TypeError('summarize expects only finite numbers');
  }

  if (numbers.length === 0) {
    return { n: 0, min: null, p50: null, p95: null, max: null, mean: null };
  }

  const sortedNumbers = [...numbers].sort((a, b) => a - b);
  const sum = numbers.reduce((total, number) => total + number, 0);

  return {
    n: numbers.length,
    min: sortedNumbers[0],
    p50: nearestRank(sortedNumbers, 0.5),
    p95: nearestRank(sortedNumbers, 0.95),
    max: sortedNumbers[sortedNumbers.length - 1],
    mean: sum / numbers.length
  };
}
