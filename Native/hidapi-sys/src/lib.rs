#![allow(non_camel_case_types, non_upper_case_globals, non_snake_case)]

// Bindings are generated at build time by build.rs using bindgen.
include!(concat!(env!("OUT_DIR"), "/bindings.rs"));
