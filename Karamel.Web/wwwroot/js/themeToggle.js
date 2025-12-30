// Simple theme toggle module
const THEME_KEY = 'karamel-theme';

export function getTheme() {
  try {
    return localStorage.getItem(THEME_KEY);
  } catch (e) {
    return null;
  }
}

export function setTheme(theme) {
  try {
    if (theme === 'dark') {
      document.documentElement.setAttribute('data-theme', 'dark');
      localStorage.setItem(THEME_KEY, 'dark');
    } else if (theme === 'light') {
      document.documentElement.removeAttribute('data-theme');
      localStorage.setItem(THEME_KEY, 'light');
    } else {
      // remove explicit preference
      document.documentElement.removeAttribute('data-theme');
      localStorage.removeItem(THEME_KEY);
    }
    return true;
  } catch (e) {
    return false;
  }
}

export function initTheme() {
  try {
    const pref = getTheme();
    if (pref === 'dark') {
      document.documentElement.setAttribute('data-theme', 'dark');
    } else if (pref === 'light') {
      document.documentElement.removeAttribute('data-theme');
    }
    return pref || '';
  } catch (e) {
    return '';
  }
}
