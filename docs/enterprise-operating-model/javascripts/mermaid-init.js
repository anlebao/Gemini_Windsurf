// mermaid init for MkDocs Material + superfences (offline file:// safe)
document.addEventListener("DOMContentLoaded", function () {
  if (typeof mermaid === "undefined") return;
  mermaid.initialize({ startOnLoad: false, securityLevel: "loose", theme: "default" });
  document.querySelectorAll("pre.mermaid, div.mermaid").forEach(function (el, i) {
    var code = el.tagName === "PRE" ? (el.querySelector("code")?.textContent || el.textContent) : el.textContent;
    var host = document.createElement("div");
    host.className = "mermaid";
    host.textContent = code;
    el.replaceWith(host);
    mermaid.run({ nodes: [host] });
  });
});
