use std::env;
// use std::path::PathBuf;

fn main() {
    // let out_dir = PathBuf::from(env::var("OUT_DIR").unwrap());
    let target_os = env::var("CARGO_CFG_TARGET_OS").unwrap();

    let mut cfg = cmake::Config::new("externals/hidapi");
    cfg.very_verbose(true);

    // Cargoのビルドプロファイルに合わせる
    if env::var("PROFILE").map(|p| p == "debug").unwrap_or(false) {
        cfg.profile("Debug");
    } else {
        cfg.profile("Release");
    }

    // 静的ライブラリにし、テスト用アプリはビルドしない
    cfg.define("BUILD_SHARED_LIBS", "OFF")
        .define("HIDAPI_BUILD_HIDTEST", "OFF")
        .define("HIDAPI_WITH_TESTS", "OFF");

    // Linuxではhidrawを使用
    if target_os.as_str() == "linux" {
        cfg.define("HIDAPI_WITH_HIDRAW", "ON")
            .define("HIDAPI_WITH_LIBUSB", "OFF");
    }

    let dst = cfg.build();
    println!("cargo:rustc-link-search=native={}/lib", dst.display());

    // 各OSに合わせてリンクするライブラリを指定
    match target_os.as_str() {
        "macos" => {
            println!("cargo:rustc-link-lib=static=hidapi");
            println!("cargo:rustc-link-lib=framework=IOKit");
            println!("cargo:rustc-link-lib=framework=CoreFoundation");
        }
        "linux" => {
            println!("cargo:rustc-link-lib=static=hidapi-hidraw");
            println!("cargo:rustc-link-lib=udev");
            println!("cargo:rustc-link-lib=pthread");
        }
        "windows" => {
            println!("cargo:rustc-link-lib=static=hidapi");
        }
        "netbsd" => {
            println!("cargo:rustc-link-lib=static=hidapi-netbsd");
            println!("cargo:rustc-link-lib=pthread");
        }
        _ => {
            println!("cargo:rustc-link-lib=static=hidapi");
        }
    }
}
