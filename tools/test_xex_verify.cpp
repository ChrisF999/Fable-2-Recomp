#include "../src/core/xex_verify.h"
#include <iostream>
#include <stdexcept>

static void Require(bool ok, const char* message) {
  if (!ok) throw std::runtime_error(message);
}

int main(int argc, char** argv) {
  using namespace fable2::xexverify;
  try {
    Require(IsAcceptedHash(kExpectedSha256), "USA/EU hash rejected");
    Require(IsAcceptedHash(kGermanGotySha256), "German hash rejected");
    Require(DefaultLanguage(kExpectedSha256) == 1, "USA/EU default language");
    Require(DefaultLanguage(kGermanGotySha256) == 3, "German default language");
    Require(!IsAcceptedHash("cec9238ef5d7b391345a8897ef00a4673ae4f106f89385c81723ba5f9d0807b5"), "retail accepted");
    const auto root = std::filesystem::temp_directory_path() /
        ("fable2-xex-test-" + std::to_string(std::chrono::steady_clock::now().time_since_epoch().count()));
    std::filesystem::create_directories(root);
    const auto fixture = root / "synthetic.xex";
    { std::ofstream out(fixture, std::ios::binary); out << "abc"; }
    std::string digest;
    Require(Sha256File(fixture, digest) && digest == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "SHA-256 abc vector");
    Require(Check(root / "missing", root / "marker").result == Result::ReadFailed, "missing file accepted");
    Require(Check(fixture, root / "marker").result == Result::Mismatch, "unknown fixture accepted");
    const auto content = root / "content";
    std::filesystem::create_directories(content / "data/language/de-de/text");
    Require(!HasCompatibleContent(content, kExpectedSha256), "missing GOTY markers accepted");
    { std::ofstream(content / "data/gold_version.txt") << "synthetic";
      std::ofstream(content / "data/startup.vfsconfig") << "synthetic"; }
    Require(HasCompatibleContent(content, kExpectedSha256), "USA/EU markers rejected");
    Require(!HasCompatibleContent(content, kGermanGotySha256), "missing German localization accepted");
    { std::ofstream(content / "data/language/de-de/text/book.babel") << "synthetic"; }
    Require(HasCompatibleContent(content, kGermanGotySha256), "German GOTY markers rejected");
    Require(!HasCompatibleContent(content, "unknown"), "unknown content profile accepted");
    { std::ofstream(content / "data/tu1_data.bnk") << "synthetic"; }
    Require(!HasCompatibleContent(content, kExpectedSha256), "mixed USA/EU content accepted");
    Require(!HasCompatibleContent(content, kGermanGotySha256), "mixed German content accepted");
    for (int i = 1; i < argc; ++i) {
      auto first = Check(argv[i], root / "marker");
      Require(first.result == Result::VerifiedFresh, "original profile hash failed or cross-root cache reused");
      Require(Check(argv[i], root / "marker").result == Result::VerifiedCached, "same-root cache failed");
      std::cout << "Verified original profile " << first.actual_hash << '\n';
    }
    // Only remove explicitly created files from the unique synthetic test folder.
    std::filesystem::remove(fixture);
    std::filesystem::remove(root / "marker");
    std::filesystem::remove(content / "data/gold_version.txt");
    std::filesystem::remove(content / "data/startup.vfsconfig");
    std::filesystem::remove(content / "data/tu1_data.bnk");
    std::filesystem::remove(content / "data/language/de-de/text/book.babel");
    std::filesystem::remove(content / "data/language/de-de/text");
    std::filesystem::remove(content / "data/language/de-de");
    std::filesystem::remove(content / "data/language");
    std::filesystem::remove(content / "data");
    std::filesystem::remove(content);
    std::filesystem::remove(root);
    std::cout << "Dual-GOTY SHA-256, language and cache tests passed\n";
  } catch (const std::exception& error) {
    std::cerr << error.what() << '\n';
    return 1;
  }
}
